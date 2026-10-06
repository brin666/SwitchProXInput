using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SwitchProXInput.App.Services;
using SwitchProXInput.Core;

namespace SwitchProXInput.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _pollTimer;
    private readonly HidMaestroOutput _output = new();
    private SdlSwitchProInput? _input;
    private SwitchInputPoll _lastPoll;
    private bool _isStarting;
    private bool _isClosingForElevation;
    private DateTime _nextUiRefreshUtc;
    private string? _inputInitializationError;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _input = new SdlSwitchProInput();
        }
        catch (Exception exception)
        {
            _inputInitializationError = exception.GetBaseException().Message;
            SetError($"SDL3 输入初始化失败：{_inputInitializationError}");
        }

        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(8)
        };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();
        UpdateUi();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.StartImmediately)
        {
            await StartConversionAsync();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e) =>
        await StartConversionAsync();

    private async Task StartConversionAsync()
    {
        if (_isStarting || _output.IsActive)
        {
            return;
        }

        if (!WindowsElevation.IsAdministrator())
        {
            var choice = MessageBox.Show(
                "Windows 要求以管理员身份创建虚拟手柄。现在重新启动并显示 UAC 授权提示吗？",
                "需要管理员授权",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.Yes);

            if (choice == MessageBoxResult.Yes)
            {
                try
                {
                    Process? elevated = WindowsElevation.RestartElevated("--start");
                    if (elevated is null)
                    {
                        throw new InvalidOperationException("Windows 没有启动管理员进程。");
                    }

                    _isClosingForElevation = true;
                    Close();
                }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
                {
                    SetError("已取消 UAC 授权，虚拟手柄没有启动。");
                }
                catch (Exception exception)
                {
                    ShowError("无法以管理员身份启动", exception);
                }
            }

            return;
        }

        _isStarting = true;
        ClearError();
        UpdateUi();
        try
        {
            await _output.StartAsync(ConfirmFirstInstall);
            ClearError();
        }
        catch (OperationCanceledException)
        {
            SetError("未启动虚拟手柄；首次组件安装已取消。");
        }
        catch (Exception exception)
        {
            ShowError("启动 XInput 虚拟手柄失败", exception);
        }
        finally
        {
            _isStarting = false;
            UpdateUi();
        }
    }

    private bool ConfirmFirstInstall()
    {
        var choice = MessageBox.Show(
            "本机尚未安装虚拟 Xbox 手柄组件。继续后，HIDMaestro 1.9.2 会安装其现有 UMDF2/XUSB 组件，并把本地签名证书加入 Windows 信任存储。此操作需要管理员授权。\n\n本工具不会改 WindowsApps 权限或游戏文件。是否继续？",
            "首次安装虚拟手柄组件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return choice == MessageBoxResult.Yes;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _output.Stop();
            ClearError();
        }
        catch (Exception exception)
        {
            ShowError("停止虚拟手柄失败", exception);
        }
        finally
        {
            UpdateUi();
        }
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            _lastPoll = _input?.Poll(DateTime.UtcNow) ?? default;
            if (_output.IsActive)
            {
                var deadzone = (float)(DeadzoneSlider.Value / 100d);
                var mapped = new SwitchProMapper(deadzone).Map(_lastPoll.State);
                _output.Submit(mapped);
            }
        }
        catch (Exception exception)
        {
            _lastPoll = default;
            var reason = exception.GetBaseException().Message;
            if (_output.IsActive)
            {
                try { _output.Stop(); } catch { }
                ShowError("输入或 XInput 输出发生错误", exception);
            }
            else
            {
                SetError($"输入检测错误：{reason}");
            }
        }

        if (DateTime.UtcNow >= _nextUiRefreshUtc)
        {
            _nextUiRefreshUtc = DateTime.UtcNow.AddMilliseconds(180);
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        if (_inputInitializationError is not null)
        {
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(190, 55, 49));
            ConnectionStatusText.Text = "输入采集不可用";
        }
        else if (_lastPoll.Connected)
        {
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(33, 154, 96));
            ConnectionStatusText.Text = $"已连接 · {_lastPoll.DeviceName ?? "Nintendo Switch Pro Controller"}";
        }
        else
        {
            ConnectionDot.Fill = new SolidColorBrush(Color.FromRgb(231, 166, 41));
            ConnectionStatusText.Text = "未检测到手柄；正在自动等待连接";
        }

        if (_isStarting)
        {
            OutputBadge.Background = new SolidColorBrush(Color.FromRgb(255, 245, 218));
            OutputBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(158, 103, 0));
            OutputBadgeText.Text = "启动中";
            OutputStatusText.Text = "正在创建 Xbox 360 虚拟手柄…";
        }
        else if (_output.IsActive)
        {
            OutputBadge.Background = new SolidColorBrush(Color.FromRgb(224, 246, 235));
            OutputBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(24, 119, 72));
            OutputBadgeText.Text = "运行中";
            OutputStatusText.Text = _lastPoll.Connected
                ? "XInput 已启动，正在转换输入"
                : "XInput 已启动，输出中性状态并等待手柄重连";
        }
        else
        {
            OutputBadge.Background = new SolidColorBrush(Color.FromRgb(238, 241, 246));
            OutputBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 138));
            OutputBadgeText.Text = "停止";
            OutputStatusText.Text = "未启动";
        }

        StartButton.IsEnabled = !_isStarting && !_output.IsActive;
        StopButton.IsEnabled = !_isStarting && _output.IsActive;
        LiveInputText.Text = _lastPoll.Connected
            ? $"输入：左摇杆 ({_lastPoll.State.LeftX:0.00}, {_lastPoll.State.LeftY:0.00})   右摇杆 ({_lastPoll.State.RightX:0.00}, {_lastPoll.State.RightY:0.00})   ZL {_lastPoll.State.LeftTrigger:P0} / ZR {_lastPoll.State.RightTrigger:P0}"
            : "输入：等待中";

        if (ErrorText.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(ErrorText.Text))
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void DeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DeadzoneValueText is not null)
        {
            DeadzoneValueText.Text = $"{DeadzoneSlider.Value:0}%";
        }
    }

    private void SetError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string title, Exception exception)
    {
        var reason = exception.GetBaseException().Message;
        SetError($"{title}：{reason}");
        MessageBox.Show($"{title}\n\n{reason}", "Switch Pro → XInput", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _pollTimer.Stop();
        try
        {
            _input?.Dispose();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"关闭 SDL 输入时发生错误：{exception.GetBaseException().Message}",
                "Switch Pro → XInput",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        if (!_isClosingForElevation)
        {
            try
            {
                _output.Dispose();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"清理虚拟 XInput 手柄失败：{exception.GetBaseException().Message}",
                    "Switch Pro → XInput",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
