using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class MainForm : Form
{
    private const int HeaderHeight = 76;
    private const int DetailsActionBarHeight = 52;

    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private readonly IStartupItemLauncher _itemLauncher;
    private readonly Action<bool>? _selfStartChanged;
    private readonly StartupConfig _config;
    private SplitContainer? _split;

    private readonly DataGridView _items = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window
    };
    private readonly Label _emptyState = new()
    {
        Dock = DockStyle.Fill,
        Text = "还没有启动项目。点击“新增脚本”或“新增程序”添加。",
        TextAlign = ContentAlignment.MiddleCenter,
        BackColor = SystemColors.Window,
        ForeColor = SystemColors.GrayText
    };

    private readonly TextBox _name = new() { Dock = DockStyle.Top };
    private readonly TextBox _path = new() { Dock = DockStyle.Top };
    private readonly TextBox _arguments = new() { Dock = DockStyle.Top };
    private readonly TextBox _workingDirectory = new() { Dock = DockStyle.Top };
    private readonly CheckBox _enabled = new() { Text = "启用此项目", Dock = DockStyle.Top };
    private readonly Label _permissionStatus = new() { AutoSize = true };
    private readonly Label _selfStartStatus = new() { AutoSize = true };
    private readonly Label _selfStartStatusDetails = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Button _selfStartToggle = new() { Width = 140, Height = 30, Text = "配置管理员自启" };
    private readonly Label _typeDisplay = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _recentStart = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _recentEnd = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _exitCode = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _lastStatus = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _lastError = new() { Dock = DockStyle.Top, AutoSize = true };

    public MainForm(StartupConfig config, ConfigStore configStore, ScheduledTaskService taskService, IStartupItemLauncher itemLauncher, Action<bool>? selfStartChanged = null)
    {
        _configStore = configStore;
        _taskService = taskService;
        _itemLauncher = itemLauncher;
        _selfStartChanged = selfStartChanged;
        _config = config;

        Text = "管理员自启动器";
        MinimumSize = new Size(ManagerWindowLayoutState.MinimumWidth, ManagerWindowLayoutState.MinimumHeight);
        Icon = AppIcon.Load();

        var layoutState = ManagerWindowLayoutState.FromConfig(_config, Screen.AllScreens.Select(screen => screen.WorkingArea).ToList());
        Size = new Size(layoutState.Width, layoutState.Height);
        if (layoutState.HasSavedBounds)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(layoutState.Left, layoutState.Top);
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        BuildLayout(layoutState.SplitterDistance);
        _selfStartToggle.Click += async (_, _) => await ConfigureSelfStartAsync();
        Activated += (_, _) => RefreshStatusLabels();
        FormClosing += (_, _) => SaveWindowLayout();
        RefreshStatusLabels();
        RefreshList();
    }

    private void BuildLayout(int splitterDistance)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = ManagerWindowLayoutState.MinimumPaneWidth,
            Panel2MinSize = ManagerWindowLayoutState.MinimumPaneWidth
        };
        _split = split;
        split.HandleCreated += (_, _) => BeginInvoke(() => ApplySplitterDistance(split, splitterDistance));
        root.Controls.Add(split, 0, 1);

        BuildLeftPane(split.Panel1);
        BuildRightPane(split.Panel2);
    }

    private static void ApplySplitterDistance(SplitContainer split, int splitterDistance)
    {
        if (split.IsDisposed || split.Width <= split.Panel1MinSize + split.Panel2MinSize)
        {
            return;
        }

        var max = split.Width - split.Panel2MinSize;
        split.SplitterDistance = Math.Min(Math.Max(splitterDistance, split.Panel1MinSize), max);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = SystemColors.ControlLightLight
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var titleArea = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        titleArea.Controls.Add(new PictureBox { Image = AppIcon.Load().ToBitmap(), SizeMode = PictureBoxSizeMode.StretchImage, Width = 32, Height = 32, Margin = new Padding(0, 8, 10, 0) });
        var titleText = new TableLayoutPanel { AutoSize = true, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 4, 0, 0) };
        titleText.Controls.Add(new Label { Text = "管理员自启动器", AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold) }, 0, 0);
        titleText.Controls.Add(new Label { Text = "登录后以管理员权限启动脚本和程序", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
        titleArea.Controls.Add(titleText);

        var statusArea = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        statusArea.Controls.Add(_selfStartToggle);
        statusArea.Controls.Add(_selfStartStatus);
        statusArea.Controls.Add(_permissionStatus);

        header.Controls.Add(titleArea, 0, 0);
        header.Controls.Add(statusArea, 1, 0);
        return header;
    }

    private void BuildLeftPane(Control parent)
    {
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        parent.Controls.Add(leftPanel);

        leftPanel.Controls.Add(new Label { Text = "启动项目", Dock = DockStyle.Fill, AutoSize = false, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var addScript = new Button { Text = "新增脚本", Width = 100 };
        var addProgram = new Button { Text = "新增程序", Width = 100 };
        var delete = new Button { Text = "删除", Width = 80 };
        addScript.Click += (_, _) => AddItem(StartupItemType.PowerShellScript);
        addProgram.Click += (_, _) => AddItem(StartupItemType.Executable);
        delete.Click += (_, _) => DeleteSelected();
        toolbar.Controls.AddRange([addScript, addProgram, delete]);
        leftPanel.Controls.Add(toolbar, 0, 1);

        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", DataPropertyName = nameof(StartupItem.Name), Width = 150 });
        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "类型", DataPropertyName = nameof(StartupItem.Type), Width = 90 });
        _items.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = nameof(StartupItem.Enabled), Width = 60 });
        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "最近状态", DataPropertyName = nameof(StartupItem.LastStatus), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _items.SelectionChanged += (_, _) => LoadSelectedIntoDetails();

        var gridHost = new Panel { Dock = DockStyle.Fill };
        gridHost.Controls.Add(_items);
        gridHost.Controls.Add(_emptyState);
        leftPanel.Controls.Add(gridHost, 0, 2);
    }

    private void BuildRightPane(Control parent)
    {
        var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DetailsActionBarHeight));
        parent.Controls.Add(rightPanel);

        rightPanel.Controls.Add(new Label { Text = "项目详情", Dock = DockStyle.Fill, AutoSize = false, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

        var detailsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 8, 0) };
        detailsScroll.Controls.Add(details);
        rightPanel.Controls.Add(detailsScroll, 0, 1);

        AddLabeled(details, "管理员自启", _selfStartStatusDetails);
        AddLabeled(details, "名称", _name);
        AddLabeled(details, "类型", _typeDisplay);
        AddLabeled(details, "路径", _path);
        AddLabeled(details, "参数", _arguments);
        AddLabeled(details, "工作目录", _workingDirectory);
        details.Controls.Add(_enabled);
        AddLabeled(details, "最近启动", _recentStart);
        AddLabeled(details, "最近结束", _recentEnd);
        AddLabeled(details, "退出码", _exitCode);
        AddLabeled(details, "状态", _lastStatus);
        AddLabeled(details, "最后错误", _lastError);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        var logs = new Button { Text = "打开日志", Width = 90 };
        var stop = new Button { Text = "停止", Width = 80 };
        var run = new Button { Text = "立即运行", Width = 90 };
        var save = new Button { Text = "保存", Width = 80 };
        save.Click += (_, _) => SaveSelected();
        run.Click += async (_, _) => await RunSelectedAsync();
        stop.Click += (_, _) => StopSelected();
        logs.Click += (_, _) => OpenSelectedLogs();
        actions.Controls.AddRange([save, run, stop, logs]);
        rightPanel.Controls.Add(actions, 0, 2);
    }

    internal void RefreshStatusLabels()
    {
        _permissionStatus.Text = WindowsPrivilege.IsCurrentProcessAdministrator() ? "当前权限：管理员" : "当前权限：普通用户";
        var selfStartState = SelfStartSetupUiState.FromConfig(_config.StartManagerAtLogin);
        _selfStartStatus.Text = selfStartState.StatusText;
        _selfStartStatusDetails.Text = selfStartState.StatusText;
        _selfStartToggle.Text = selfStartState.ButtonText;
        _selfStartToggle.Visible = selfStartState.ButtonVisible;
        _selfStartToggle.Enabled = selfStartState.ButtonEnabled;
    }

    private async Task ConfigureSelfStartAsync()
    {
        if (_config.StartManagerAtLogin)
        {
            RefreshStatusLabels();
            return;
        }

        var result = WindowsPrivilege.IsCurrentProcessAdministrator()
            ? await _taskService.CreateOrUpdateManagerSelfStartTaskAsync(Application.ExecutablePath)
            : await _taskService.EnableManagerSelfStartElevatedAsync(Application.ExecutablePath);
        if (result.Succeeded)
        {
            _config.StartManagerAtLogin = true;
            _configStore.Save(_config);
            _selfStartChanged?.Invoke(true);
            RefreshStatusLabels();
        }
        else
        {
            MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "配置管理员开机自启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void AddLabeled(Control parent, string label, Control control)
    {
        parent.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
        parent.Controls.Add(control);
    }

    private void AddItem(StartupItemType type)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = type == StartupItemType.PowerShellScript ? "PowerShell 脚本 (*.ps1)|*.ps1" : "可执行程序 (*.exe)|*.exe"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var item = new StartupItem
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            Type = type,
            Path = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
            Enabled = true
        };
        item.EnsureTaskName();
        _config.Items.Add(item);
        _configStore.Save(_config);
        RefreshList();
        _items.CurrentCell = _items.Rows[_items.Rows.Count - 1].Cells[0];
    }

    private StartupItem? SelectedItem => _items.SelectedRows.Count == 0 ? null : _items.SelectedRows[0].DataBoundItem as StartupItem;

    private void RefreshList()
    {
        _items.DataSource = null;
        _items.DataSource = _config.Items;
        _emptyState.Visible = _config.Items.Count == 0;
        _emptyState.BringToFront();
    }

    private void LoadSelectedIntoDetails()
    {
        var item = SelectedItem;
        if (item is null) return;
        _name.Text = item.Name;
        _path.Text = item.Path;
        _arguments.Text = item.Arguments;
        _workingDirectory.Text = item.WorkingDirectory;
        _enabled.Checked = item.Enabled;
        _typeDisplay.Text = item.Type == StartupItemType.PowerShellScript ? "PowerShell 脚本" : "可执行程序";
        _recentStart.Text = item.LastRunStartedAt.HasValue ? item.LastRunStartedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "";
        _recentEnd.Text = item.LastRunFinishedAt.HasValue ? item.LastRunFinishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "";
        _exitCode.Text = item.LastExitCode?.ToString() ?? "";
        _lastStatus.Text = item.LastStatus.ToString();
        _lastError.Text = item.LastTaskError;
    }

    private void SaveSelected()
    {
        var item = SelectedItem;
        if (item is null) return;
        string? selectedId = item.Id;
        item.Name = _name.Text.Trim();
        item.Path = _path.Text.Trim();
        item.Arguments = _arguments.Text;
        item.WorkingDirectory = _workingDirectory.Text.Trim();
        item.Enabled = _enabled.Checked;

        var validation = StartupItemValidator.Validate(item);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _configStore.Save(_config);
        RefreshList();
        RestoreSelection(selectedId);
        LoadSelectedIntoDetails();
    }

    private void RestoreSelection(string? selectedId)
    {
        if (selectedId is null) return;
        var match = _config.Items.FirstOrDefault(x => x.Id == selectedId);
        if (match is null) return;
        foreach (DataGridViewRow row in _items.Rows)
        {
            if (row.DataBoundItem == match)
            {
                row.Selected = true;
                break;
            }
        }
    }

    private void DeleteSelected()
    {
        var item = SelectedItem;
        if (item is null) return;
        var confirmed = MessageBox.Show(this, $"确定删除\u201c{item.Name}\u201d吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmed != DialogResult.Yes) return;
        _config.Items.Remove(item);
        _configStore.Save(_config);
        RefreshList();
    }

    private async Task RunSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _itemLauncher.RunAsync(_config, item);
        RefreshList();
        LoadSelectedIntoDetails();
    }

    private void StopSelected()
    {
        MessageBox.Show(this, "当前版本不跟踪已启动进程，无法可靠停止。请在任务管理器或目标程序中结束。", "无法停止", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenSelectedLogs()
    {
        var item = SelectedItem;
        if (item is null) return;
        var directory = AppPaths.GetItemLogDirectory(item.Id);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    private void SaveWindowLayout()
    {
        if (_split is null) return;
        try
        {
            ManagerWindowLayoutState.SaveToConfig(_config, this, _split);
            _configStore.Save(_config);
        }
        catch
        {
            // Layout persistence must not block closing the manager.
        }
    }
}
