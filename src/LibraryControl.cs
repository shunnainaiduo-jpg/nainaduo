using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace CadLibraryManager;

internal sealed class LibraryControl : UserControl
{
    private const int ThumbnailBatchSize = 16;

    private readonly Color _leftBack = Color.FromArgb(231, 235, 240);
    private readonly Color _mainBack = Color.FromArgb(238, 241, 245);
    private readonly Color _surfaceBack = Color.FromArgb(226, 231, 237);
    private readonly Color _inputBack = Color.FromArgb(246, 248, 250);
    private readonly Color _border = Color.FromArgb(196, 204, 214);
    private readonly Color _accent = Color.FromArgb(35, 116, 224);

    private LibraryDatabase? _database;
    private readonly Dictionary<string, Image> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TreeView _folderTree = new();
    private readonly ListView _blockGrid = new();
    private readonly ImageList _thumbnailImages = new();
    private readonly object _thumbnailCacheLock = new();
    private readonly System.Windows.Forms.Timer _filterTimer = new();
    private readonly ComboBox _rootSelector = new();
    private readonly ComboBox _categoryFilterBox = new();
    private readonly ContextMenuStrip _blockMenu = new();
    private readonly Label _pathLabel = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _searchBox = new();
    private readonly Button _chooseFolderButton = new();
    private readonly Button _leftRefreshButton = new();
    private readonly Button _toolbarRefreshButton = new();
    private readonly Button _toolbarImportButton = new();
    private readonly Button _makeBlockButton = new();
    private readonly Button _thumbnailButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _renameButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _insertButton = new();
    private readonly CheckBox _followScaleBox = new();
    private readonly CheckBox _rotateBox = new();
    private readonly CheckBox _repeatBox = new();
    private readonly CheckBox _asBlockBox = new();
    private readonly CheckBox _explodeBox = new();
    private readonly CheckBox _editAttributesBox = new();
    private readonly CheckBox _currentLayerBox = new();
    private readonly CheckBox _handleConflictBox = new();
    private readonly NumericUpDown _scaleBox = new();
    private readonly NumericUpDown _rotationBox = new();
    private readonly TextBox _layerBox = new();
    private readonly TextBox _displayNameBox = new();
    private readonly TextBox _categoryBox = new();
    private readonly TextBox _tagsBox = new();
    private readonly CheckBox _favoriteBox = new();
    private readonly CheckBox _favoriteOnlyBox = new();
    private readonly CheckBox _recentOnlyBox = new();

    private List<string> _libraryRoots = new();
    private CancellationTokenSource? _folderLoadCts;
    private CancellationTokenSource? _thumbnailLoadCts;
    private CancellationTokenSource? _thumbnailGenerationCts;
    private CancellationTokenSource? _treeLoadCts;
    private Image? _placeholderThumbnail;
    private bool _bindingRootSelector;
    private bool _resettingFilters;
    private bool _restoringViewState;
    private bool _suppressFolderSelect;
    private string _rootFolder = string.Empty;
    private string _currentFolder = string.Empty;
    private LibraryViewState _viewState = LibrarySettings.GetViewState();
    private List<LibraryItem> _items = new();

    public LibraryControl()
    {
        BuildLayout();
        try
        {
            _database = new LibraryDatabase();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"初始化图库数据库失败: {ex.Message}", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        LoadLibrary();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _folderLoadCts?.Cancel();
            _folderLoadCts?.Dispose();
            _thumbnailLoadCts?.Cancel();
            _thumbnailLoadCts?.Dispose();
            _thumbnailGenerationCts?.Cancel();
            _thumbnailGenerationCts?.Dispose();
            _treeLoadCts?.Cancel();
            _treeLoadCts?.Dispose();
            _filterTimer.Dispose();
            ClearThumbnailCache();
            _placeholderThumbnail?.Dispose();
            _thumbnailImages.Dispose();
            _blockMenu.Dispose();
            _database?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;
        BackColor = _mainBack;
        Font = new Font("Microsoft YaHei UI", 9F);

        _thumbnailImages.ImageSize = new Size(118, 88);
        _thumbnailImages.ColorDepth = ColorDepth.Depth32Bit;
        _placeholderThumbnail = CreatePlaceholderThumbnail();
        _blockGrid.LargeImageList = _thumbnailImages;
        _blockGrid.Dock = DockStyle.Fill;
        _blockGrid.View = View.LargeIcon;
        _blockGrid.BorderStyle = BorderStyle.None;
        _blockGrid.BackColor = _mainBack;
        _blockGrid.ForeColor = Color.FromArgb(43, 53, 66);
        _blockGrid.MultiSelect = false;
        _blockGrid.HideSelection = false;
        _blockGrid.FullRowSelect = true;
        _blockGrid.SelectedIndexChanged += (_, _) =>
        {
            ShowSelectedMetadata();
            SaveViewState();
        };
        _blockGrid.DoubleClick += (_, _) => InsertSelected();
        _blockGrid.MouseUp += BlockGrid_MouseUp;

        _filterTimer.Interval = 250;
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilterAndSaveState();
        };

        BuildContextMenu();

        var leftPanel = CreateLeftPanel();
        var contentPanel = CreateContentPanel();
        Controls.Add(contentPanel);
        Controls.Add(leftPanel);
    }

    private Control CreateLeftPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 285,
            BackColor = _leftBack,
            Padding = new Padding(12)
        };

        var title = new Label
        {
            Text = "云图库 / 本地图库",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 43, 54)
        };

        _folderTree.Dock = DockStyle.Fill;
        _folderTree.BorderStyle = BorderStyle.None;
        _folderTree.BackColor = _leftBack;
        _folderTree.ForeColor = Color.FromArgb(43, 53, 66);
        _folderTree.LineColor = Color.FromArgb(172, 181, 194);
        _folderTree.HideSelection = false;
        _folderTree.BeforeExpand += (_, e) => LoadTreeNodeChildren(e.Node);
        _folderTree.AfterSelect += (_, e) =>
        {
            if (_suppressFolderSelect)
            {
                return;
            }

            SelectFolder(e.Node?.Tag as string);
        };

        _rootSelector.Dock = DockStyle.Top;
        _rootSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _rootSelector.Height = 32;
        _rootSelector.Margin = new Padding(0, 0, 0, 8);
        ConfigureField(_rootSelector);
        _rootSelector.SelectedIndexChanged += (_, _) => SwitchLibraryRoot();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = _leftBack
        };
        ConfigureButton(_chooseFolderButton, "加载本地图库", 110, true);
        ConfigureIconButton(_leftRefreshButton, "刷新");
        _chooseFolderButton.Click += (_, _) => ChooseFolder();
        _leftRefreshButton.Click += (_, _) => LoadLibrary(cleanupMissingFiles: true);
        actions.Controls.AddRange(new Control[] { _chooseFolderButton, _leftRefreshButton });

        var options = CreateInsertOptionsPanel();
        options.Dock = DockStyle.Fill;
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 248,
            BackColor = _leftBack
        };
        bottomPanel.Controls.Add(options);
        bottomPanel.Controls.Add(actions);

        panel.Controls.Add(_folderTree);
        panel.Controls.Add(bottomPanel);
        panel.Controls.Add(_rootSelector);
        panel.Controls.Add(title);
        return panel;
    }

    private Control CreateContentPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _mainBack,
            Padding = new Padding(14)
        };

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = _mainBack };
        _pathLabel.Dock = DockStyle.Top;
        _pathLabel.Height = 28;
        _pathLabel.ForeColor = Color.FromArgb(87, 99, 115);
        _pathLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);

        var searchRow = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = _mainBack };
        ConfigureButton(_toolbarImportButton, "新增", 62, false);
        ConfigureButton(_makeBlockButton, "制作", 62, false);
        ConfigureButton(_thumbnailButton, "缩略图", 74, false);
        ConfigureButton(_toolbarRefreshButton, "刷新", 62, false);
        var viewButton = CreateToolbarButton("视图");
        ConfigureButton(_openFolderButton, "定位", 62, false);
        ConfigureButton(_insertButton, "插入", 74, true);
        ConfigureButton(_renameButton, "重命名", 74, false);
        ConfigureButton(_saveButton, "保存属性", 82, false);
        _insertButton.Click += (_, _) => InsertSelected();
        _renameButton.Click += (_, _) => RenameSelected();
        _saveButton.Click += (_, _) => SaveSelectedMetadata();
        _toolbarImportButton.Click += (_, _) => ImportFiles();
        _makeBlockButton.Click += (_, _) => MakeBlockFromSelection();
        _thumbnailButton.Click += (_, _) => GenerateMissingThumbnails();
        _toolbarRefreshButton.Click += (_, _) => LoadFolder(_currentFolder, cleanupMissingFiles: true);
        viewButton.Click += (_, _) => ToggleGridView();
        _openFolderButton.Click += (_, _) => OpenSelectedFolder();

        _searchBox.Width = 260;
        _searchBox.Height = 28;
        _searchBox.Margin = new Padding(8, 8, 8, 0);
        ConfigureField(_searchBox);
        _searchBox.TextChanged += (_, _) => ScheduleFilter();

        _favoriteOnlyBox.Text = "只看收藏";
        _favoriteOnlyBox.Width = 78;
        _favoriteOnlyBox.Margin = new Padding(4, 12, 4, 0);
        _favoriteOnlyBox.BackColor = _mainBack;
        _favoriteOnlyBox.ForeColor = Color.FromArgb(65, 78, 94);
        _favoriteOnlyBox.CheckedChanged += (_, _) => ApplyFilterAndSaveState();

        ConfigureFilterCombo(_categoryFilterBox, 118);
        _categoryFilterBox.SelectedIndexChanged += (_, _) => ApplyFilterAndSaveState();

        _recentOnlyBox.Text = "最近使用";
        _recentOnlyBox.Width = 78;
        _recentOnlyBox.Margin = new Padding(4, 12, 4, 0);
        _recentOnlyBox.BackColor = _mainBack;
        _recentOnlyBox.ForeColor = Color.FromArgb(65, 78, 94);
        _recentOnlyBox.CheckedChanged += (_, _) => ApplyFilterAndSaveState();

        var searchPlaceholder = new Label
        {
            Text = "搜索本地图块",
            AutoSize = true,
            ForeColor = Color.FromArgb(130, 140, 153),
            Margin = new Padding(0, 13, 6, 0)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = _mainBack
        };
        buttons.Controls.Add(_toolbarImportButton);
        buttons.Controls.Add(_makeBlockButton);
        buttons.Controls.Add(_thumbnailButton);
        buttons.Controls.Add(viewButton);
        buttons.Controls.Add(searchPlaceholder);
        buttons.Controls.Add(_searchBox);
        buttons.Controls.Add(_categoryFilterBox);
        buttons.Controls.Add(_favoriteOnlyBox);
        buttons.Controls.Add(_recentOnlyBox);
        buttons.Controls.Add(_toolbarRefreshButton);
        buttons.Controls.Add(_openFolderButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_renameButton);
        buttons.Controls.Add(_insertButton);
        searchRow.Controls.Add(buttons);

        toolbar.Controls.Add(searchRow);
        toolbar.Controls.Add(_pathLabel);

        var details = CreateMetadataPanel();
        panel.Controls.Add(_blockGrid);
        panel.Controls.Add(details);
        panel.Controls.Add(toolbar);
        return panel;
    }

    private Control CreateInsertOptionsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 208,
            BackColor = _leftBack,
            Padding = new Padding(0, 4, 0, 0)
        };

        var title = new Label
        {
            Text = "插入选项",
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = Color.FromArgb(80, 92, 108),
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold)
        };

        _followScaleBox.Text = "跟随比例";
        _rotateBox.Text = "旋转";
        _repeatBox.Text = "重复放置";
        _asBlockBox.Text = "成块";
        _explodeBox.Text = "可分解";
        _editAttributesBox.Text = "编辑属性";
        _currentLayerBox.Text = "当前图层";
        _handleConflictBox.Text = "处理定义冲突";
        _followScaleBox.Checked = true;
        _asBlockBox.Checked = true;
        _currentLayerBox.Checked = true;

        var checks = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = _leftBack
        };

        foreach (var box in new[] { _followScaleBox, _rotateBox, _repeatBox, _asBlockBox, _explodeBox, _editAttributesBox, _currentLayerBox, _handleConflictBox })
        {
            box.Width = 92;
            box.Height = 21;
            box.Margin = new Padding(0, 0, 4, 0);
            box.BackColor = _leftBack;
            box.ForeColor = Color.FromArgb(65, 78, 94);
            checks.Controls.Add(box);
        }

        _scaleBox.Minimum = 1;
        _scaleBox.Maximum = 1000;
        _scaleBox.Value = 100;
        _scaleBox.Width = 72;
        _rotationBox.Minimum = -360;
        _rotationBox.Maximum = 360;
        _rotationBox.Width = 72;
        _layerBox.Width = 152;

        var inputs = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, BackColor = _leftBack };
        inputs.Controls.Add(CreateSmallInput("比例%", _scaleBox));
        inputs.Controls.Add(CreateSmallInput("角度", _rotationBox));
        inputs.Controls.Add(CreateSmallInput("图层", _layerBox));

        panel.Controls.Add(checks);
        panel.Controls.Add(inputs);
        panel.Controls.Add(title);
        return panel;
    }

    private Control CreateMetadataPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = _surfaceBack,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 8, 0, 0)
        };
        _displayNameBox.Width = 142;
        _categoryBox.Width = 92;
        _tagsBox.Width = 150;
        _favoriteBox.Height = 26;
        _favoriteBox.Text = "收藏";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(100, 112, 128);
        ConfigureField(_displayNameBox);
        ConfigureField(_categoryBox);
        ConfigureField(_tagsBox);
        _favoriteBox.BackColor = _surfaceBack;
        _favoriteBox.ForeColor = Color.FromArgb(65, 78, 94);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = _surfaceBack };
        row.Controls.Add(CreateCompactInput("显示", _displayNameBox));
        row.Controls.Add(CreateCompactInput("分类", _categoryBox));
        row.Controls.Add(CreateCompactInput("标签", _tagsBox));
        row.Controls.Add(_favoriteBox);
        row.Controls.Add(_statusLabel);
        panel.Controls.Add(row);
        return panel;
    }

    private Control CreateCompactInput(string label, Control input)
    {
        var panel = new Panel { Width = input.Width + 8, Height = 38, Margin = new Padding(0, 0, 6, 0), BackColor = _surfaceBack };
        var labelControl = new Label { Text = label, Dock = DockStyle.Top, Height = 14, ForeColor = Color.FromArgb(100, 112, 128) };
        labelControl.BackColor = _surfaceBack;
        input.Dock = DockStyle.Bottom;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private Control CreateSmallInput(string label, Control input)
    {
        ConfigureField(input);
        var panel = new Panel { Width = input.Width + 12, Height = 46, Margin = new Padding(0, 0, 6, 0), BackColor = _leftBack };
        var labelControl = new Label { Text = label, Dock = DockStyle.Top, Height = 16, ForeColor = Color.FromArgb(100, 112, 128) };
        labelControl.BackColor = _leftBack;
        input.Dock = DockStyle.Bottom;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private Button CreateToolbarButton(string text)
    {
        var button = new Button();
        ConfigureButton(button, text, 62, false);
        return button;
    }

    private void ConfigureButton(Button button, string text, int width, bool primary)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 30;
        button.Margin = new Padding(3, 7, 3, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderColor = primary ? _accent : _border;
        button.BackColor = primary ? _accent : _surfaceBack;
        button.ForeColor = primary ? Color.White : Color.FromArgb(62, 76, 94);
    }

    private void ConfigureIconButton(Button button, string text)
    {
        ConfigureButton(button, text, 54, false);
    }

    private void ConfigureFilterCombo(ComboBox comboBox, int width)
    {
        comboBox.Width = width;
        comboBox.Height = 28;
        comboBox.Margin = new Padding(4, 8, 4, 0);
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        ConfigureField(comboBox);
    }

    private void ConfigureField(Control control)
    {
        control.BackColor = _inputBack;
        control.ForeColor = Color.FromArgb(43, 53, 66);
    }

    private void LoadLibrary(bool cleanupMissingFiles = false)
    {
        _libraryRoots = LibrarySettings.GetLibraryRoots().Where(Directory.Exists).ToList();
        if (_libraryRoots.Count == 0)
        {
            _libraryRoots.Add(LibrarySettings.GetLibraryFolder());
        }

        _viewState = LibrarySettings.GetViewState();
        var preferredRoot = !string.IsNullOrWhiteSpace(_viewState.RootFolder)
            ? _viewState.RootFolder
            : LibrarySettings.GetLibraryFolder();
        _rootFolder = _libraryRoots.FirstOrDefault(root => string.Equals(root, preferredRoot, StringComparison.OrdinalIgnoreCase)) ?? _libraryRoots[0];
        RebindRootSelector();
        Directory.CreateDirectory(_rootFolder);
        _currentFolder = IsFolderUnderRoot(_viewState.CurrentFolder, _rootFolder) && Directory.Exists(_viewState.CurrentFolder)
            ? _viewState.CurrentFolder
            : _rootFolder;
        BuildFolderTree();
        RestoreFilterState();
        LoadFolder(_currentFolder, cleanupMissingFiles);
    }

    private void RebindRootSelector()
    {
        _bindingRootSelector = true;
        _rootSelector.Items.Clear();
        foreach (var root in _libraryRoots)
        {
            _rootSelector.Items.Add(GetRootDisplayName(root));
        }

        var selectedIndex = _libraryRoots.FindIndex(root => string.Equals(root, _rootFolder, StringComparison.OrdinalIgnoreCase));
        _rootSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _bindingRootSelector = false;
    }

    private void SwitchLibraryRoot()
    {
        if (_bindingRootSelector)
        {
            return;
        }

        if (_rootSelector.SelectedIndex < 0 || _rootSelector.SelectedIndex >= _libraryRoots.Count)
        {
            return;
        }

        var selectedRoot = _libraryRoots[_rootSelector.SelectedIndex];
        if (string.Equals(selectedRoot, _rootFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _rootFolder = selectedRoot;
        LibrarySettings.SaveLibraryFolder(_rootFolder);
        _folderLoadCts?.Cancel();
        ClearThumbnailCache();
        ResetFilters();
        BuildFolderTree();
        LoadFolder(_rootFolder);
    }

    private void BuildFolderTree()
    {
        _treeLoadCts?.Cancel();
        _treeLoadCts?.Dispose();
        _treeLoadCts = new CancellationTokenSource();

        _folderTree.Nodes.Clear();
        var rootName = new DirectoryInfo(_rootFolder).Name;
        var root = new TreeNode(string.IsNullOrWhiteSpace(rootName) ? "02图库" : rootName) { Tag = _rootFolder };
        AddPlaceholderNode(root);
        _folderTree.Nodes.Add(root);
        _suppressFolderSelect = true;
        try
        {
            root.Expand();
            _folderTree.SelectedNode = root;
        }
        finally
        {
            _suppressFolderSelect = false;
        }
    }

    private async void LoadTreeNodeChildren(TreeNode? node)
    {
        if (node?.Tag is not string folder || node.Nodes.Count == 0 || node.Nodes[0].Tag is not null)
        {
            return;
        }

        var cancellationToken = _treeLoadCts?.Token ?? CancellationToken.None;
        node.Nodes.Clear();
        try
        {
            var directories = await Task.Run(() => EnumerateDirectories(folder)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(), cancellationToken);

            if (cancellationToken.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            foreach (var directory in directories)
            {
                var child = new TreeNode(Path.GetFileName(directory)) { Tag = directory };
                AddPlaceholderNode(child);
                node.Nodes.Add(child);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void AddPlaceholderNode(TreeNode node)
    {
        node.Nodes.Add(new TreeNode("...") { Tag = null });
    }

    private CancellationTokenSource ResetFolderLoader()
    {
        _folderLoadCts?.Cancel();
        _folderLoadCts?.Dispose();
        _folderLoadCts = new CancellationTokenSource();
        return _folderLoadCts;
    }

    private sealed class FolderLoadResult
    {
        public required string Folder { get; init; }
        public required List<string> Files { get; init; }
    }

    private static FolderLoadResult LoadFolderFiles(string folder, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        foreach (var file in EnumerateDwgFiles(folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(file);
        }

        return new FolderLoadResult { Folder = folder, Files = files };
    }

    private void SelectFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        _currentFolder = folder;
        SaveViewState();
        LoadFolder(folder);
    }

    private async void LoadFolder(string folder, bool cleanupMissingFiles = false)
    {
        try
        {
            var folderLoadCts = ResetFolderLoader();
            var cancellationToken = folderLoadCts.Token;
            _items = new List<LibraryItem>();
            _pathLabel.Text = $"本地图库: {MakeDisplayPath(folder)}";
            _statusLabel.Text = "  正在扫描...";
            PopulateGrid(Array.Empty<LibraryItem>());

            FolderLoadResult result;
            try
            {
                result = await Task.Run(() => LoadFolderFiles(folder, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested || !string.Equals(result.Folder, _currentFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (cleanupMissingFiles)
            {
                _database?.RemoveMissingFiles(folder, result.Files);
            }
            var metadataByPath = _database?.GetMany(result.Files);
            _items = result.Files.Select(path => new LibraryItem(path, GetMetadata(path, metadataByPath)))
                .OrderByDescending(item => item.IsFavorite)
                .ThenByDescending(item => item.Metadata.LastUsedAt ?? DateTime.MinValue)
                .ThenBy(item => item.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            RebindFilterOptions();
            RestoreFilterSelections();
            ApplyFilter();
            RestoreSelectedItem();
            SaveViewState();
        }
        catch (Exception ex)
        {
            _items = new List<LibraryItem>();
            _statusLabel.Text = "扫描失败";
            MessageBox.Show(this, $"扫描图库目录失败: {ex.Message}", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        if (_resettingFilters)
        {
            return;
        }

        var keyword = _searchBox.Text.Trim();
        IEnumerable<LibraryItem> query = _items;

        if (_recentOnlyBox.Checked)
        {
            query = query.Where(item => item.Metadata.LastUsedAt.HasValue)
                .OrderByDescending(item => item.Metadata.LastUsedAt ?? DateTime.MinValue);
        }

        if (_favoriteOnlyBox.Checked)
        {
            query = query.Where(item => item.IsFavorite);
        }

        var selectedCategory = _categoryFilterBox.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(selectedCategory) && selectedCategory != "全部分类")
        {
            query = query.Where(item => string.Equals(item.Category, selectedCategory, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item => item.SearchText.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        PopulateGrid(query);
    }

    private void ScheduleFilter()
    {
        if (_resettingFilters)
        {
            return;
        }

        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void RebindFilterOptions()
    {
        var categories = _items.Select(item => item.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var currentCategory = _categoryFilterBox.SelectedItem as string;

        _categoryFilterBox.Items.Clear();
        _categoryFilterBox.Items.Add("全部分类");
        _categoryFilterBox.Items.AddRange(categories.Cast<object>().ToArray());
        _categoryFilterBox.SelectedItem = categories.Contains(currentCategory ?? string.Empty, StringComparer.CurrentCultureIgnoreCase) ? currentCategory : "全部分类";
    }

    private void RestoreFilterSelections()
    {
        _restoringViewState = true;
        _resettingFilters = true;
        if (!string.IsNullOrWhiteSpace(_viewState.Category) && _categoryFilterBox.Items.Contains(_viewState.Category))
        {
            _categoryFilterBox.SelectedItem = _viewState.Category;
        }

        _resettingFilters = false;
        _restoringViewState = false;
    }

    private void PopulateGrid(IEnumerable<LibraryItem> items)
    {
        var visibleItems = items.ToList();
        var thumbnailCts = ResetThumbnailLoader();
        _blockGrid.BeginUpdate();
        _blockGrid.Items.Clear();
        _thumbnailImages.Images.Clear();
        _thumbnailImages.Images.Add("__placeholder", _placeholderThumbnail ?? CreatePlaceholderThumbnail());
        var listItems = new ListViewItem[visibleItems.Count];
        var itemIndex = 0;
        foreach (var item in visibleItems)
        {
            var cachedThumbnail = TryGetCachedThumbnail(item.FilePath);
            var imageKey = cachedThumbnail == null ? "__placeholder" : item.FilePath;
            if (cachedThumbnail != null)
            {
                _thumbnailImages.Images.Add(imageKey, cachedThumbnail);
            }

            listItems[itemIndex++] = new ListViewItem(item.ToString(), imageKey) { Tag = item, ToolTipText = item.FilePath };
        }

        _blockGrid.Items.AddRange(listItems);

        SelectVisibleItem(_viewState.SelectedFilePath);

        _blockGrid.EndUpdate();
        _statusLabel.Text = visibleItems.Count == 0 && _items.Count > 0
            ? $"  当前筛选无结果 / 共 {_items.Count} 个图块"
            : $"  显示 {visibleItems.Count} / {_items.Count} 个图块";
        QueueThumbnailLoading(visibleItems, thumbnailCts.Token);
    }

    private void RestoreSelectedItem()
    {
        if (_blockGrid.Items.Count == 0)
        {
            return;
        }

        SelectVisibleItem(_viewState.SelectedFilePath);
    }

    private void SelectVisibleItem(string? filePath)
    {
        if (_blockGrid.Items.Count == 0)
        {
            return;
        }

        ListViewItem? itemToSelect = null;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            foreach (ListViewItem listViewItem in _blockGrid.Items)
            {
                if (listViewItem.Tag is LibraryItem item && string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    itemToSelect = listViewItem;
                    break;
                }
            }
        }

        itemToSelect ??= _blockGrid.Items[0];
        itemToSelect.Selected = true;
        itemToSelect.Focused = true;
        itemToSelect.EnsureVisible();
    }

    private Image? TryGetCachedThumbnail(string filePath)
    {
        lock (_thumbnailCacheLock)
        {
            return _thumbnailCache.TryGetValue(filePath, out var cached) ? cached : null;
        }
    }

    private CancellationTokenSource ResetThumbnailLoader()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = new CancellationTokenSource();
        return _thumbnailLoadCts;
    }

    private void QueueThumbnailLoading(IReadOnlyList<LibraryItem> items, CancellationToken cancellationToken)
    {
        var pendingItems = items.Where(item => TryGetCachedThumbnail(item.FilePath) == null).ToList();
        if (pendingItems.Count == 0)
        {
            return;
        }

        _ = Task.Run(() => LoadThumbnails(pendingItems, cancellationToken), cancellationToken);
    }

    private void LoadThumbnails(IEnumerable<LibraryItem> items, CancellationToken cancellationToken)
    {
        var loadedBatch = new List<(string FilePath, Image Thumbnail)>(ThumbnailBatchSize);
        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var thumbnail = DwgPreviewReader.ReadSidecarPreview(item.FilePath);
            if (thumbnail == null)
            {
                continue;
            }

            lock (_thumbnailCacheLock)
            {
                if (_thumbnailCache.ContainsKey(item.FilePath))
                {
                    thumbnail.Dispose();
                    continue;
                }

                _thumbnailCache[item.FilePath] = thumbnail;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            loadedBatch.Add((item.FilePath, thumbnail));
            if (loadedBatch.Count >= ThumbnailBatchSize && !FlushLoadedThumbnails(loadedBatch, cancellationToken))
            {
                return;
            }
        }

        FlushLoadedThumbnails(loadedBatch, cancellationToken);
    }

    private bool FlushLoadedThumbnails(List<(string FilePath, Image Thumbnail)> loadedBatch, CancellationToken cancellationToken)
    {
        if (loadedBatch.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        var thumbnails = loadedBatch.ToArray();
        loadedBatch.Clear();
        try
        {
            BeginInvoke(new Action(() => ApplyLoadedThumbnails(thumbnails, cancellationToken)));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void ApplyLoadedThumbnails(IReadOnlyList<(string FilePath, Image Thumbnail)> thumbnails, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        var updated = false;
        _blockGrid.BeginUpdate();
        try
        {
            var loadedByPath = thumbnails.ToDictionary(item => item.FilePath, item => item.Thumbnail, StringComparer.OrdinalIgnoreCase);
            foreach (var (filePath, thumbnail) in thumbnails)
            {
                var imageIndex = _thumbnailImages.Images.IndexOfKey(filePath);
                if (imageIndex < 0)
                {
                    _thumbnailImages.Images.Add(filePath, thumbnail);
                }
                else
                {
                    _thumbnailImages.Images[imageIndex] = thumbnail;
                    _thumbnailImages.Images.SetKeyName(imageIndex, filePath);
                }
            }

            foreach (ListViewItem listViewItem in _blockGrid.Items)
            {
                if (listViewItem.Tag is not LibraryItem item || !loadedByPath.ContainsKey(item.FilePath))
                {
                    continue;
                }

                listViewItem.ImageKey = item.FilePath;
                updated = true;
            }
        }
        finally
        {
            _blockGrid.EndUpdate();
        }

        if (updated)
        {
            _blockGrid.Invalidate();
        }
    }

    private Image CreatePlaceholderThumbnail()
    {
        var bitmap = new Bitmap(118, 88);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(_surfaceBack);
        using var borderPen = new Pen(_border);
        using var accentPen = new Pen(Color.FromArgb(104, 129, 171), 2F);
        graphics.DrawRectangle(borderPen, 0, 0, 117, 87);
        graphics.DrawLine(accentPen, 22, 60, 96, 22);
        graphics.DrawRectangle(accentPen, 30, 28, 56, 34);
        using var brush = new SolidBrush(Color.FromArgb(68, 82, 100));
        using var font = new Font(Font.FontFamily, 7.5F);
        graphics.DrawString("DWG", font, brush, 8, 8);
        return bitmap;
    }

    private void ShowSelectedMetadata()
    {
        if (GetSelectedItem() is not { } item)
        {
            _displayNameBox.Text = string.Empty;
            _categoryBox.Text = string.Empty;
            _tagsBox.Text = string.Empty;
            _favoriteBox.Checked = false;
            return;
        }

        _displayNameBox.Text = item.Name;
        _categoryBox.Text = item.Category;
        _tagsBox.Text = item.Tags;
        _favoriteBox.Checked = item.IsFavorite;
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择本地图库根目录", SelectedPath = _rootFolder };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        LibrarySettings.SaveLibraryFolder(dialog.SelectedPath);
        _libraryRoots = LibrarySettings.GetLibraryRoots();
        ResetFilters();
        LoadLibrary();
    }

    private void ResetFilters()
    {
        _resettingFilters = true;
        _searchBox.Text = string.Empty;
        _favoriteOnlyBox.Checked = false;
        _recentOnlyBox.Checked = false;
        _categoryFilterBox.SelectedItem = "全部分类";
        _resettingFilters = false;
    }

    private static IEnumerable<string> EnumerateDwgFiles(string folder)
    {
        return EnumerateFilesSafe(folder, "*.dwg");
    }

    private void RestoreFilterState()
    {
        _restoringViewState = true;
        _resettingFilters = true;
        _searchBox.Text = _viewState.SearchText ?? string.Empty;
        _favoriteOnlyBox.Checked = _viewState.FavoriteOnly;
        _recentOnlyBox.Checked = _viewState.RecentOnly;
        _resettingFilters = false;
        _restoringViewState = false;
    }

    private void ApplyFilterAndSaveState()
    {
        ApplyFilter();
        SaveViewState();
    }

    private void SaveViewState()
    {
        if (_restoringViewState)
        {
            return;
        }

        _viewState.RootFolder = _rootFolder;
        _viewState.CurrentFolder = _currentFolder;
        _viewState.SearchText = _searchBox.Text;
        _viewState.Category = _categoryFilterBox.SelectedItem as string ?? string.Empty;
        _viewState.Tag = string.Empty;
        _viewState.FavoriteOnly = _favoriteOnlyBox.Checked;
        _viewState.RecentOnly = _recentOnlyBox.Checked;
        _viewState.SelectedFilePath = GetSelectedItem()?.FilePath ?? _viewState.SelectedFilePath;
        LibrarySettings.SaveViewState(_viewState);
    }

    private static bool IsFolderUnderRoot(string folder, string root)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalizedFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedFolder.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateDirectories(string folder)
    {
        return EnumerateDirectoriesSafe(folder);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string folder, string searchPattern)
    {
        foreach (var file in EnumerateFilesInDirectorySafe(folder, searchPattern))
        {
            yield return file;
        }

        foreach (var directory in EnumerateDirectoriesSafe(folder))
        {
            foreach (var file in EnumerateFilesSafe(directory, searchPattern))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string folder)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = Directory.EnumerateDirectories(folder).GetEnumerator();
        }
        catch
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                string directory;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        yield break;
                    }

                    directory = enumerator.Current;
                }
                catch
                {
                    yield break;
                }

                yield return directory;
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesInDirectorySafe(string folder, string searchPattern)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = Directory.EnumerateFiles(folder, searchPattern).GetEnumerator();
        }
        catch
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                string file;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        yield break;
                    }

                    file = enumerator.Current;
                }
                catch
                {
                    yield break;
                }

                yield return file;
            }
        }
    }

    private void ImportFiles()
    {
        using var dialog = new OpenFileDialog { Filter = "DWG 文件 (*.dwg)|*.dwg", Multiselect = true, Title = "批量导入 DWG 图块" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Directory.CreateDirectory(_currentFolder);
        foreach (var sourcePath in dialog.FileNames)
        {
            var targetPath = GetUniquePath(Path.Combine(_currentFolder, Path.GetFileName(sourcePath)));
            File.Copy(sourcePath, targetPath);
            CopySidecarPreview(sourcePath, targetPath);
            _database?.GetOrCreate(targetPath);
        }
        LoadFolder(_currentFolder);
    }

    private static void CopySidecarPreview(string sourceDwgPath, string targetDwgPath)
    {
        var sourcePreviewPath = Path.ChangeExtension(sourceDwgPath, ".png");
        if (!File.Exists(sourcePreviewPath))
        {
            return;
        }

        File.Copy(sourcePreviewPath, Path.ChangeExtension(targetDwgPath, ".png"), overwrite: true);
    }

    private void MakeBlockFromSelection()
    {
        var savedPath = BlockMaker.SaveSelectionAsDwg(_currentFolder, GetUniquePath);
        if (string.IsNullOrWhiteSpace(savedPath))
        {
            return;
        }

        _database?.GetOrCreate(savedPath);
        LoadFolder(_currentFolder);
    }

    private void RenameSelected()
    {
        if (GetSelectedItem() is not { } item)
        {
            MessageBox.Show(this, "请先选择一个 DWG 图块。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newName = PromptText.ShowDialog("输入新的文件名，不需要 .dwg 后缀", "重命名", item.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        var safeName = Path.GetFileNameWithoutExtension(newName.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "文件名为空或包含非法字符。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var targetPath = GetUniquePath(Path.Combine(Path.GetDirectoryName(item.FilePath) ?? _currentFolder, safeName + ".dwg"));
        File.Move(item.FilePath, targetPath);
        MoveSidecarPreview(item.FilePath, targetPath);
        RemoveThumbnailCache(item.FilePath);
        _database?.RenamePath(item.FilePath, targetPath, Path.GetFileNameWithoutExtension(targetPath));
        LoadFolder(_currentFolder);
    }

    private static void MoveSidecarPreview(string oldDwgPath, string newDwgPath)
    {
        var oldPreviewPath = Path.ChangeExtension(oldDwgPath, ".png");
        if (!File.Exists(oldPreviewPath))
        {
            return;
        }

        var newPreviewPath = Path.ChangeExtension(newDwgPath, ".png");
        if (File.Exists(newPreviewPath))
        {
            File.Delete(newPreviewPath);
        }

        File.Move(oldPreviewPath, newPreviewPath);
    }

    private void SaveSelectedMetadata()
    {
        if (GetSelectedItem() is not { } item)
        {
            return;
        }
        if (_database == null)
        {
            MessageBox.Show(this, "数据库不可用，无法保存元数据。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        item.Metadata.DisplayName = _displayNameBox.Text.Trim();
        item.Metadata.Category = _categoryBox.Text.Trim();
        item.Metadata.Tags = _tagsBox.Text.Trim();
        item.Metadata.IsFavorite = _favoriteBox.Checked;
        _database.Save(item.Metadata);
        item.RefreshCaches();
        RebindFilterOptions();
        ApplyFilter();
    }

    private void InsertSelected()
    {
        if (GetSelectedItem() is not { } item)
        {
            MessageBox.Show(this, "请先选择一个 DWG 图块。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        BlockInserter.InsertDwgAsBlock(item.FilePath, new InsertOptions
        {
            Scale = _followScaleBox.Checked ? (double)_scaleBox.Value / 100.0 : 1.0,
            RotationDegrees = _rotateBox.Checked ? (double)_rotationBox.Value : 0,
            RotateOnInsert = _rotateBox.Checked,
            LayerName = _currentLayerBox.Checked ? string.Empty : _layerBox.Text.Trim(),
            RepeatPlacement = _repeatBox.Checked,
            InsertAsBlock = _asBlockBox.Checked,
            AllowExplode = _explodeBox.Checked,
            EditAttributes = _editAttributesBox.Checked,
            UseCurrentLayer = _currentLayerBox.Checked,
            CreateUniqueBlockOnConflict = _handleConflictBox.Checked
        });

        _database?.MarkAsUsed(item.FilePath);
        item.Metadata.LastUsedAt = DateTime.Now;
        ApplyFilter();
    }

    private void OpenSelectedFolder()
    {
        if (GetSelectedItem() is not { } item)
        {
            return;
        }

        var folder = Path.GetDirectoryName(item.FilePath);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start("explorer.exe", folder);
        }
    }

    private void BuildContextMenu()
    {
        _blockMenu.Items.Add("插入", null, (_, _) => InsertSelected());
        _blockMenu.Items.Add("重新生成缩略图", null, (_, _) => RegenerateSelectedThumbnail());
        _blockMenu.Items.Add("保存属性", null, (_, _) => SaveSelectedMetadata());
        _blockMenu.Items.Add("重命名", null, (_, _) => RenameSelected());
        _blockMenu.Items.Add("打开所在目录", null, (_, _) => OpenSelectedFolder());
        _blockMenu.Items.Add("切换收藏", null, (_, _) => ToggleFavoriteSelected());
        _blockMenu.Items.Add("移除当前图库", null, (_, _) => RemoveCurrentLibraryRoot());
    }

    private void BlockGrid_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var item = _blockGrid.GetItemAt(e.X, e.Y);
        if (item != null)
        {
            item.Selected = true;
        }

        _blockMenu.Show(_blockGrid, e.Location);
    }

    private void RegenerateSelectedThumbnail()
    {
        if (GetSelectedItem() is not { } item)
        {
            MessageBox.Show(this, "请先选择一个 DWG 图块。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!DwgPreviewReader.GeneratePreviewPng(item.FilePath, overwrite: true))
        {
            MessageBox.Show(this, "生成缩略图失败。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RemoveThumbnailCache(item.FilePath);
        LoadFolder(_currentFolder);
    }

    private async void GenerateMissingThumbnails()
    {
        if (_thumbnailGenerationCts != null)
        {
            _thumbnailGenerationCts.Cancel();
            return;
        }

        var candidates = _items
            .Where(item => !File.Exists(Path.ChangeExtension(item.FilePath, ".png")))
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, "当前目录的缩略图已经补齐。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var generated = 0;
        _thumbnailGenerationCts = new CancellationTokenSource();
        var cancellationToken = _thumbnailGenerationCts.Token;
        var oldButtonText = _thumbnailButton.Text;
        _thumbnailButton.Text = "鍙栨秷";
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var item = candidates[index];
                _statusLabel.Text = $"  姝ｅ湪鐢熸垚缂╃暐鍥?{index + 1} / {candidates.Count}";
                await Task.Yield();
                if (DwgPreviewReader.GeneratePreviewPng(item.FilePath, overwrite: false))
                {
                    generated++;
                    RemoveThumbnailCache(item.FilePath);
                }
            }
        }
        finally
        {
            Cursor.Current = Cursors.Default;
            _thumbnailButton.Text = oldButtonText;
            _thumbnailGenerationCts.Dispose();
            _thumbnailGenerationCts = null;
        }

        LoadFolder(_currentFolder);
        MessageBox.Show(this, $"已生成 {generated} / {candidates.Count} 个缩略图。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RemoveThumbnailCache(string filePath)
    {
        Image? image = null;
        lock (_thumbnailCacheLock)
        {
            if (_thumbnailCache.Remove(filePath, out var removedImage))
            {
                image = removedImage;
            }
        }

        if (image != null)
        {
            image.Dispose();
        }
    }

    private void ClearThumbnailCache()
    {
        List<Image> images;
        lock (_thumbnailCacheLock)
        {
            images = _thumbnailCache.Values.ToList();
            _thumbnailCache.Clear();
        }

        foreach (var image in images)
        {
            image.Dispose();
        }
    }

    private void ToggleFavoriteSelected()
    {
        if (GetSelectedItem() is not { } item || _database == null)
        {
            return;
        }

        item.Metadata.IsFavorite = !item.Metadata.IsFavorite;
        _database.Save(item.Metadata);
        RebindFilterOptions();
        ApplyFilter();
    }

    private void RemoveCurrentLibraryRoot()
    {
        if (_libraryRoots.Count <= 1)
        {
            MessageBox.Show(this, "至少保留一个图库根目录。", "CAD 图库管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, $"确定移除图库根目录？\n{_rootFolder}", "CAD 图库管理", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        LibrarySettings.RemoveLibraryRoot(_rootFolder);
        _libraryRoots = LibrarySettings.GetLibraryRoots();
        _rootFolder = _libraryRoots[0];
        LoadLibrary();
    }

    private LibraryItem? GetSelectedItem() => _blockGrid.SelectedItems.Count == 0 ? null : _blockGrid.SelectedItems[0].Tag as LibraryItem;

    private LibraryMetadata GetMetadata(string path, Dictionary<string, LibraryMetadata>? metadataByPath = null)
    {
        if (metadataByPath != null && metadataByPath.TryGetValue(Path.GetFullPath(path).Trim(), out var metadata))
        {
            return metadata;
        }

        if (_database != null)
        {
            return CreateTransientMetadata(path);
        }

        return CreateTransientMetadata(path);
    }

    private static LibraryMetadata CreateTransientMetadata(string path)
    {
        return new LibraryMetadata { Id = path, FilePath = path, DisplayName = Path.GetFileNameWithoutExtension(path) };
    }

    private string MakeDisplayPath(string folder)
    {
        var relative = GetRelativePath(_rootFolder, folder);
        var rootName = GetRootDisplayName(_rootFolder);
        return relative == "." ? $"\\{rootName}" : $"\\{rootName}\\{relative.Replace('/', '\\')}";
    }

    private static string GetRelativePath(string basePath, string targetPath)
    {
        var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(basePath)));
        var targetUri = new Uri(Path.GetFullPath(targetPath));
        var relativeUri = baseUri.MakeRelativeUri(targetUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath) ? "." : relativePath;
    }

    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;
    }

    private static string GetRootDisplayName(string root)
    {
        var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? root : name;
    }

    private void ToggleGridView()
    {
        _blockGrid.View = _blockGrid.View == View.LargeIcon ? View.Tile : View.LargeIcon;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            index++;
        }
    }
}
