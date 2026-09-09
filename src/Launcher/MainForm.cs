using System.Drawing;
using System.Windows.Forms;

namespace Nota.ThoriumVoiceBridge;

internal sealed class MainForm : Form
{
    private readonly VoiceBridgeService service;
    private readonly ListView leftList = CreateListView();
    private readonly ListView rightList = CreateListView();
    private readonly CheckBox danishOnly = new() { Text = "Vis kun Dansk", AutoSize = true };
    private readonly Button actionButton = new() { Text = "—", Enabled = false };
    private readonly Label statusLabel = new() { AutoSize = false };
    private readonly RichTextBox logBox = new() { ReadOnly = true, Visible = false };
    private readonly Label leftLabel = new() { Text = "Ikke i Thorium", AutoSize = true };
    private readonly Label rightLabel = new() { Text = "I Thorium", AutoSize = true };
    private readonly Size normalSize = new(988, 453);
    private readonly Size logSize = new(980, 580);

    public MainForm(VoiceBridgeService service, string baseDirectory)
    {
        this.service = service;
        Text = "Tilføj stemmer til Thorium";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        KeyPreview = true;
        ClientSize = normalSize;
        Font = new Font("Segoe UI", 9);

        var iconPath = Path.Combine(baseDirectory, "KB_icon.ico");
        if (File.Exists(iconPath))
        {
            try { Icon = new Icon(iconPath); } catch (Exception) { }
        }

        var logo = new PictureBox
        {
            Location = new Point(770, 15),
            Size = new Size(190, 55),
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        var logoPath = Path.Combine(baseDirectory, "KB_logo.png");
        if (File.Exists(logoPath))
        {
            try { logo.Image = Image.FromFile(logoPath); } catch (Exception) { }
        }

        var title = new Label
        {
            Text = "Tilføj stemmer til Thorium",
            Location = new Point(12, 8),
            Size = new Size(500, 24),
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };

        statusLabel.Location = new Point(12, 34);
        statusLabel.Size = new Size(740, 36);
        danishOnly.Location = new Point(12, 74);
        leftLabel.Location = new Point(12, 100);
        rightLabel.Location = new Point(520, 100);
        ConfigureList(leftList, new Point(12, 122));
        ConfigureList(rightList, new Point(520, 122));

        actionButton.Location = new Point(458, 202);
        actionButton.Size = new Size(56, 120);
        actionButton.Font = new Font("Segoe UI", 8);
        logBox.Location = new Point(12, 417);
        logBox.Size = new Size(948, 140);

        Controls.AddRange([title, statusLabel, logo, danishOnly, leftLabel, rightLabel, leftList, rightList, actionButton, logBox]);
        WireEvents();
        Shown += (_, _) => RefreshLists();
        KeyDown += OnKeyDown;
    }

    private static ListView CreateListView() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false,
        MultiSelect = true,
        CheckBoxes = true,
        Size = new Size(440, 280)
    };

    private static void ConfigureList(ListView list, Point location)
    {
        list.Location = location;
        list.Columns.Add("Navn");
        list.Columns.Add("Leverandør");
        list.Columns.Add("Sprog");
    }

    private void WireEvents()
    {
        leftList.ItemCheck += (_, args) => OnItemCheck(leftList, args, canSelect: true);
        rightList.ItemCheck += (_, args) => OnItemCheck(rightList, args, canSelect: false);
        leftList.ItemChecked += (_, _) => UpdateActionButton();
        rightList.ItemChecked += (_, _) => UpdateActionButton();
        danishOnly.CheckedChanged += (_, _) => RefreshLists();
        actionButton.Click += (_, _) => RunAction();
    }

    private void OnItemCheck(ListView source, ItemCheckEventArgs args, bool canSelect)
    {
        if (args.Index < 0 || args.Index >= source.Items.Count)
        {
            return;
        }

        var row = (VoiceRow)source.Items[args.Index].Tag!;
        if (!row.CanSelect && args.NewValue == CheckState.Checked)
        {
            args.NewValue = CheckState.Unchecked;
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        if (args.NewValue == CheckState.Checked)
        {
            UncheckAll(canSelect ? rightList : leftList);
        }
    }

    private void RefreshLists()
    {
        leftList.Items.Clear();
        rightList.Items.Clear();
        SetStatus();

        var oneCore = service.GetOneCoreVoices()
            .Where(voice => !voice.IsMicrosoft)
            .Where(voice => !danishOnly.Checked || voice.LanguageHex == "0406")
            .OrderBy(voice => voice.Vendor)
            .ThenBy(voice => voice.Name)
            .ThenBy(voice => voice.TokenName);
        var oneCoreNames = oneCore.Select(voice => voice.TokenName).ToHashSet(StringComparer.Ordinal);

        foreach (var voice in oneCore)
        {
            AddRow(rightList, voice, voice.IsBridged);
        }

        var sapi = service.GetSapiVoices()
            .Where(voice => !voice.IsMicrosoft)
            .Where(voice => !oneCoreNames.Contains(voice.TokenName))
            .Where(voice => !danishOnly.Checked || voice.LanguageHex == "0406")
            .OrderBy(voice => voice.Vendor)
            .ThenBy(voice => voice.Name)
            .ThenBy(voice => voice.TokenName);

        foreach (var voice in sapi)
        {
            AddRow(leftList, voice, canSelect: true);
        }

        SetEqualColumnWidths(leftList);
        SetEqualColumnWidths(rightList);
        UpdateActionButton();
        Log($"Opdateret. Ikke i Thorium: {leftList.Items.Count} | I Thorium: {rightList.Items.Count}");
    }

    private static void AddRow(ListView list, VoiceToken voice, bool canSelect)
    {
        var item = new ListViewItem(voice.Name ?? voice.DisplayName);
        item.SubItems.Add(voice.Vendor ?? string.Empty);
        item.SubItems.Add(VoiceBridgeService.LanguageLabel(voice.Language));
        item.Tag = new VoiceRow(voice, canSelect);
        if (!canSelect)
        {
            item.ForeColor = Color.Gray;
        }
        list.Items.Add(item);
    }

    private void RunAction()
    {
        var source = leftList.CheckedItems.Count > 0 ? leftList : rightList;
        var rows = source.CheckedItems.Cast<ListViewItem>().Select(item => (VoiceRow)item.Tag!).ToArray();
        var changed = 0;

        foreach (var row in rows)
        {
            try
            {
                var result = source == leftList
                    ? service.InstallVoice(row.Voice.TokenName)
                    : service.RemoveVoice(row.Voice.TokenName);
                if (result.Changed) changed++;
                Log(result.Message);
            }
            catch (Exception error)
            {
                Log($"FEJL ({row.Voice.TokenName}): {error.Message}");
            }
        }

        RefreshLists();
        if (changed > 0)
        {
            MessageBox.Show(
                source == leftList
                    ? "Stemmen er tilføjet til Thorium. Genstart Thorium for at bruge den nye stemme."
                    : "Stemmen er fjernet fra Thorium. Genstart Thorium for at bruge ændringen.",
                "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void UpdateActionButton()
    {
        var leftChecked = leftList.CheckedItems.Count;
        var rightChecked = rightList.CheckedItems.Count;
        if (leftChecked > 0 && rightChecked > 0)
        {
            actionButton.Text = "—";
            actionButton.Enabled = false;
        }
        else if (leftChecked > 0)
        {
            actionButton.Text = "Flyt\n→";
            actionButton.Enabled = true;
        }
        else if (rightChecked > 0)
        {
            actionButton.Text = "←\nFjern";
            actionButton.Enabled = true;
        }
        else
        {
            actionButton.Text = "—";
            actionButton.Enabled = false;
        }
    }

    private void SetStatus()
    {
        statusLabel.Text = "Tilføj eller fjern oplæsningsstemmer, så de kan bruges i Thorium Reader.";
        statusLabel.ForeColor = SystemColors.ControlText;
    }

    private void UncheckAll(ListView list)
    {
        foreach (ListViewItem item in list.Items)
        {
            item.Checked = false;
        }
    }

    private void SetEqualColumnWidths(ListView list)
    {
        var width = (list.ClientSize.Width - 16) / list.Columns.Count;
        foreach (ColumnHeader column in list.Columns)
        {
            column.Width = width;
        }
    }

    private void Log(string message)
    {
        logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.KeyCode == Keys.F5)
        {
            RefreshLists();
            args.Handled = true;
        }
    }

    private sealed record VoiceRow(VoiceToken Voice, bool CanSelect);
}
