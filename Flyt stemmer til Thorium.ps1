# Tilføj stemmer til Thorium (v1.3)
# Skrevet af Joshua Lindemann for Det Kgl. Biblioteks Nota-service
#
# Windows PowerShell 5.1 compatible

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$SapiRoot    = "HKLM:\SOFTWARE\Microsoft\Speech\Voices\Tokens"
$OneCoreRoot = "HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens"
$BridgeMarkerName  = "ThoriumBridge"
$BridgeMarkerValue = "1"

# ---------------- Helpers ----------------

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Normalize-LangHex([string]$lang) {
    if ([string]::IsNullOrWhiteSpace($lang)) { return $null }

    $t = $lang.Trim()

    # Hvis det ligner hex (1-4 tegn), så pad til 4 og upper-case
    if ($t -match '^[0-9A-Fa-f]{1,4}$') {
        return $t.PadLeft(4,'0').ToUpper()
    }

    # Hvis det er decimalt (LCID), så konvertér til hex og pad til 4
    if ($t -match '^[0-9]{1,5}$') {
        try {
            return ('{0:X4}' -f [int]$t)
        } catch {
            return $t
        }
    }

    return $t
}

function Get-LangLabel([string]$langHex) {
    switch ($langHex.ToUpper()) {

        # Nordiske
        "0406" { "Dansk (DA)" }
        "041D" { "Svensk (SV)" }
        "0414" { "Norsk Bokmål (NO)" }
        "0814" { "Norsk Nynorsk (NO)" }
        "040B" { "Finsk (FI)" }
        "040F" { "Islandsk (IS)" }

        # Engelsk
        "0409" { "Engelsk (US)" }
        "0809" { "Engelsk (UK)" }
        "0C09" { "Engelsk (AU)" }
        "1009" { "Engelsk (CA)" }
        "1409" { "Engelsk (NZ)" }
        "1809" { "Engelsk (IE)" }

        # Tysk
        "0407" { "Tysk (DE)" }
        "0807" { "Tysk (CH)" }
        "0C07" { "Tysk (AT)" }

        # Fransk
        "040C" { "Fransk (FR)" }
        "080C" { "Fransk (BE)" }
        "0C0C" { "Fransk (CA)" }
        "100C" { "Fransk (CH)" }

        # Spansk
        "040A" { "Spansk (ES)" }
        "080A" { "Spansk (MX)" }
        "0C0A" { "Spansk (ES – modern)" }
        "140A" { "Spansk (CR)" }
        "180A" { "Spansk (PA)" }
        "1C0A" { "Spansk (DO)" }
        "200A" { "Spansk (VE)" }
        "240A" { "Spansk (CO)" }
        "280A" { "Spansk (PE)" }
        "2C0A" { "Spansk (AR)" }
        "300A" { "Spansk (EC)" }
        "340A" { "Spansk (CL)" }
        "380A" { "Spansk (UY)" }
        "3C0A" { "Spansk (PY)" }
        "400A" { "Spansk (BO)" }

        # Portugisisk
        "0416" { "Portugisisk (BR)" }
        "0816" { "Portugisisk (PT)" }

        # Italiensk
        "0410" { "Italiensk (IT)" }
        "0810" { "Italiensk (CH)" }

        # Hollandsk
        "0413" { "Hollandsk (NL)" }
        "0813" { "Hollandsk (BE)" }

        # Østeuropæisk
        "0405" { "Tjekkisk (CS)" }
        "0415" { "Polsk (PL)" }
        "0418" { "Rumænsk (RO)" }
        "0419" { "Russisk (RU)" }
        "0424" { "Slovensk (SL)" }
        "041B" { "Slovakisk (SK)" }
        "040E" { "Ungarsk (HU)" }
        "041A" { "Kroatisk (HR)" }
        "0422" { "Ukrainsk (UK)" }
        "0425" { "Estisk (ET)" }
        "0426" { "Lettisk (LV)" }
        "0427" { "Litauisk (LT)" }

        # Asiatisk
        "0411" { "Japansk (JA)" }
        "0412" { "Koreansk (KO)" }
        "0804" { "Kinesisk (CN)" }
        "0404" { "Kinesisk (TW)" }
        "0C04" { "Kinesisk (HK)" }
        "1004" { "Kinesisk (SG)" }
        "041E" { "Thai (TH)" }
        "042A" { "Vietnamesisk (VI)" }

        # Mellemøstlig
        "0401" { "Arabisk (SA)" }
        "040D" { "Hebræisk (HE)" }
        "0429" { "Persisk (FA)" }
        "041F" { "Tyrkisk (TR)" }

        default {
            if ([string]::IsNullOrWhiteSpace($langHex)) { "" }
            else { $langHex }
        }
    }
}

function Is-MicrosoftVendor([string]$vendor) {
    if ([string]::IsNullOrWhiteSpace($vendor)) { return $false }
    return ($vendor -match "^\s*Microsoft\b")
}

function Ensure-Key([string]$path) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
}

function Make-DisplayName([string]$name, [string]$vendor) {
    $n = $name; if ($null -eq $n) { $n = "" }
    $v = $vendor; if ($null -eq $v) { $v = "" }
    $n = $n.Trim()
    $v = $v.Trim()
    if ([string]::IsNullOrWhiteSpace($n)) { $n = "Ukendt" }
    if ([string]::IsNullOrWhiteSpace($v)) { return $n }
    return "$n ($v)"
}

function Log([string]$msg) {
    if (-not $txtLog) { return }
    $ts = (Get-Date).ToString("HH:mm:ss")
    $txtLog.AppendText("[$ts] $msg`r`n")
    $txtLog.SelectionStart = $txtLog.TextLength
    $txtLog.ScrollToCaret()
}

function Set-EqualColumnWidths {
    param(
        [Parameter(Mandatory=$true)]
        [System.Windows.Forms.ListView]$ListView,

        # Hvor meget vi trækker fra til scrollbar/borders (kan justeres)
        [int]$Padding = 16
    )

    $colCount = $ListView.Columns.Count
    if ($colCount -le 0) { return }

    $usable = $ListView.ClientSize.Width - $Padding
    if ($usable -lt $colCount) { return }

    $w = [Math]::Floor($usable / $colCount)

    foreach ($col in $ListView.Columns) {
        $col.Width = $w
    }
}

# ---------------- Read registry ----------------

function Get-SapiVoices {
    $result = @()
    if (-not (Test-Path $SapiRoot)) { return $result }

    foreach ($k in Get-ChildItem $SapiRoot) {
        $tokenPath = $k.PSPath
        $attrPath  = Join-Path $tokenPath "Attributes"

        $token = Get-ItemProperty -Path $tokenPath -ErrorAction SilentlyContinue
        $attr  = Get-ItemProperty -Path $attrPath  -ErrorAction SilentlyContinue

        $langHex = Normalize-LangHex $attr.Language

        $display = $null
        if ($token) { $display = $token.'(default)' }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = $attr.Name }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = $k.PSChildName }

        $name = $attr.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = $display }

        $result += [PSCustomObject]@{
            TokenName   = $k.PSChildName
            Name        = $name
            Vendor      = $attr.Vendor
            LanguageHex = $langHex
            CLSID       = if ($token) { $token.CLSID } else { $null }
        }
    }
    return $result
}

function Get-OneCoreVoices {
    $result = @()
    if (-not (Test-Path $OneCoreRoot)) { return $result }

    foreach ($k in Get-ChildItem $OneCoreRoot) {
        $tokenPath = $k.PSPath
        $attrPath  = Join-Path $tokenPath "Attributes"

        $token = Get-ItemProperty -Path $tokenPath -ErrorAction SilentlyContinue
        $attr  = Get-ItemProperty -Path $attrPath  -ErrorAction SilentlyContinue

        $langHex = $null
        if ($attr -and $attr.Language) { $langHex = Normalize-LangHex $attr.Language }

        $display = $null
        if ($token) { $display = $token.'(default)' }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = $attr.Name }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = $k.PSChildName }

        $result += [PSCustomObject]@{
            TokenName     = $k.PSChildName
            Name          = $attr.Name
            Vendor        = $attr.Vendor
            LanguageHex   = $langHex
            CLSID         = if ($token) { $token.CLSID } else { $null }
            Display       = $display
            ThoriumBridge = if ($attr) { $attr.$BridgeMarkerName } else { $null }
        }
    }
    return $result
}

function Find-OneCoreByTokenName($oneCoreVoices, [string]$tokenName) {
    return ($oneCoreVoices | Where-Object { $_.TokenName -eq $tokenName } | Select-Object -First 1)
}

# ---------------- Write registry ----------------

function Install-VoiceToOneCore([string]$tokenName) {
    $sapiTokenPath = Join-Path $SapiRoot $tokenName
    $sapiAttrPath  = Join-Path $sapiTokenPath "Attributes"
    if (-not (Test-Path $sapiTokenPath)) { throw "SAPI token findes ikke: $tokenName" }

    $token = Get-ItemProperty -Path $sapiTokenPath -ErrorAction Stop
    $attr  = Get-ItemProperty -Path $sapiAttrPath  -ErrorAction SilentlyContinue

    if ($attr -and (Is-MicrosoftVendor $attr.Vendor)) {
        Log "Springer over Microsoft-stemme: $tokenName"
        return
    }

    $langHex = Normalize-LangHex $attr.Language
    if (-not $langHex) { $langHex = "0000" }

    $oneCoreTokenPath = Join-Path $OneCoreRoot $tokenName
    $oneCoreAttrPath  = Join-Path $oneCoreTokenPath "Attributes"

    if (Test-Path $oneCoreTokenPath) {
        Log "Findes allerede i Thorium: $tokenName"
        return
    }

    Ensure-Key $oneCoreTokenPath
    Ensure-Key $oneCoreAttrPath

    $name = $attr.Name
    if ([string]::IsNullOrWhiteSpace($name)) { $name = $token.'(default)' }
    $vendor = $attr.Vendor
    $display = Make-DisplayName -name $name -vendor $vendor

    New-ItemProperty -Path $oneCoreTokenPath -Name "(default)" -Value $display -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $oneCoreTokenPath -Name $langHex     -Value $display -PropertyType String -Force | Out-Null
    if ($token.CLSID) {
        New-ItemProperty -Path $oneCoreTokenPath -Name "CLSID" -Value $token.CLSID -PropertyType String -Force | Out-Null
    }

    if ($attr.Age)    { New-ItemProperty -Path $oneCoreAttrPath -Name "Age"    -Value $attr.Age    -PropertyType String -Force | Out-Null }
    if ($attr.Gender) { New-ItemProperty -Path $oneCoreAttrPath -Name "Gender" -Value $attr.Gender -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $oneCoreAttrPath -Name "Language" -Value $langHex -PropertyType String -Force | Out-Null
    if ($name)   { New-ItemProperty -Path $oneCoreAttrPath -Name "Name"   -Value $name   -PropertyType String -Force | Out-Null }
    if ($vendor) { New-ItemProperty -Path $oneCoreAttrPath -Name "Vendor" -Value $vendor -PropertyType String -Force | Out-Null }

    New-ItemProperty -Path $oneCoreAttrPath -Name $BridgeMarkerName -Value $BridgeMarkerValue -PropertyType String -Force | Out-Null
    Log "Flyttet til Thorium: $tokenName -> '$display'"
}

function Remove-OneCoreToken([string]$tokenName) {
    $psPath  = Join-Path $OneCoreRoot $tokenName
    $regPath = "HKLM\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens\$tokenName"

    if (-not (Test-Path $psPath)) { return $true }

    try { Remove-Item -Path $psPath -Recurse -Force -ErrorAction Stop } catch {}
    Start-Sleep -Milliseconds 150
    if (-not (Test-Path $psPath)) { return $true }

    try { $null = & reg.exe delete $regPath /f 2>&1 } catch {}
    Start-Sleep -Milliseconds 150
    return (-not (Test-Path $psPath))
}

function Remove-FromThorium([string]$tokenName) {
    $attrPath = Join-Path (Join-Path $OneCoreRoot $tokenName) "Attributes"
    if (-not (Test-Path $attrPath)) {
        Log "Fjern: $tokenName mangler Attributes – springer over."
        return $false
    }

    $attr = Get-ItemProperty -Path $attrPath -ErrorAction SilentlyContinue
    if ($attr -and $attr.$BridgeMarkerName -eq $BridgeMarkerValue) {
        $ok = Remove-OneCoreToken $tokenName
        if ($ok) { Log "Fjernet fra Thorium: $tokenName" } else { Log "Fjern fejlede: $tokenName findes stadig." }
        return $ok
    }

    Log "Fjern: $tokenName er ikke markeret (native/ukendt) – kan ikke fjernes."
    return $false
}

# ---------------- GUI ----------------

$form = New-Object System.Windows.Forms.Form
$form.Text = "Tilføj stemmer til Thorium"
$form.StartPosition = "CenterScreen"

#-------------Program-ikon--------------
$baseDir  = if ($PSScriptRoot) { $PSScriptRoot } elseif ($PSCommandPath) { Split-Path -Parent $PSCommandPath } else { (Get-Location).Path }
$iconPath = Join-Path $baseDir "KB_ikon.ico"

$form.ShowIcon = $true
$form.ShowInTaskbar = $true

if ((Test-Path $iconPath)) {
    try {
        $form.Icon = [System.Drawing.Icon]::new($iconPath)
    } catch {
        # Gør intet.
    }
}

# sizes (with/without log)
$SizeNoLog = New-Object System.Drawing.Size(988, 453)
$SizeWithLog = New-Object System.Drawing.Size(980, 580)
$form.Size = $SizeNoLog
$form.KeyPreview = $true

# Lås vinduesstørrelsen
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox    = $false
$form.MinimizeBox    = $true

$font = New-Object System.Drawing.Font("Segoe UI", 9)

$lblTop = New-Object System.Windows.Forms.Label
$lblTop.AutoSize = $false
$lblTop.Location = New-Object System.Drawing.Point(12, 34)
$lblTop.Size = New-Object System.Drawing.Size(740, 36)
$lblTop.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$form.Controls.Add($lblTop)

# Logo (øverst til højre)
$logoPath = Join-Path (Split-Path -Parent $PSCommandPath) "KB_logo.png"
$picLogo = New-Object System.Windows.Forms.PictureBox
$picLogo.Location = New-Object System.Drawing.Point(770, 15)
$picLogo.Size = New-Object System.Drawing.Size(190, 55)
$picLogo.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$picLogo.Anchor = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Right
if (Test-Path $logoPath) { $picLogo.Image = [System.Drawing.Image]::FromFile($logoPath) }
$form.Controls.Add($picLogo)

# Toggles
$chkDanishOnly = New-Object System.Windows.Forms.CheckBox
$chkDanishOnly.Text = "Vis kun Dansk"
$chkDanishOnly.Location = New-Object System.Drawing.Point(12, 74)   # <-- var 62
$chkDanishOnly.Size = New-Object System.Drawing.Size(140, 20)
$chkDanishOnly.Font = $font
$form.Controls.Add($chkDanishOnly)

# Bypass visning af MS-stemmer
#$chkShowMicrosoft = New-Object System.Windows.Forms.CheckBox
#$chkShowMicrosoft.Text = "Vis Microsoft-stemmer"
#$chkShowMicrosoft.Location = New-Object System.Drawing.Point(160, 74)  # <-- var 62
#$chkShowMicrosoft.Size = New-Object System.Drawing.Size(170, 20)
#$chkShowMicrosoft.Font = $font
#$form.Controls.Add($chkShowMicrosoft)

# Bypass log
#$chkShowLog = New-Object System.Windows.Forms.CheckBox
#$chkShowLog.Text = "Vis log"
#$chkShowLog.Location = New-Object System.Drawing.Point(340, 74)        # <-- var 62
#$chkShowLog.Size = New-Object System.Drawing.Size(90, 20)
#$chkShowLog.Font = $font
#$form.Controls.Add($chkShowLog)

$lblLeft = New-Object System.Windows.Forms.Label
$lblLeft.Text = "Ikke i Thorium"
$lblLeft.Location = New-Object System.Drawing.Point(12, 100)  # <-- var 88
$lblLeft.Size = New-Object System.Drawing.Size(200, 20)
$lblLeft.Font = $font
$form.Controls.Add($lblLeft)

$lblRight = New-Object System.Windows.Forms.Label
$lblRight.Text = "I Thorium"
$lblRight.Location = New-Object System.Drawing.Point(520, 100) # <-- var 88
$lblRight.Size = New-Object System.Drawing.Size(200, 20)
$lblRight.Font = $font
$form.Controls.Add($lblRight)

# Left list (SAPI)
$listLeft = New-Object System.Windows.Forms.ListView
$listLeft.Location = New-Object System.Drawing.Point(12, 122)  # <-- var 110
$listLeft.Size = New-Object System.Drawing.Size(440, 280)
$listLeft.View = [System.Windows.Forms.View]::Details
$listLeft.FullRowSelect = $true
$listLeft.GridLines = $true
$listLeft.Font = $font
$listLeft.HideSelection = $false
$listLeft.MultiSelect = $true
$listLeft.CheckBoxes = $true
[void]$listLeft.Columns.Add("Navn", 0)
[void]$listLeft.Columns.Add("Leverandør", 0)
[void]$listLeft.Columns.Add("Sprog", 0)
$form.Controls.Add($listLeft)

# Right list (OneCore)
$listRight = New-Object System.Windows.Forms.ListView
$listRight.Location = New-Object System.Drawing.Point(520, 122) # <-- var 110
$listRight.Size = New-Object System.Drawing.Size(440, 280)
$listRight.View = [System.Windows.Forms.View]::Details
$listRight.FullRowSelect = $true
$listRight.GridLines = $true
$listRight.Font = $font
$listRight.HideSelection = $false
$listRight.MultiSelect = $true
$listRight.CheckBoxes = $true
[void]$listRight.Columns.Add("Navn", 0)
[void]$listRight.Columns.Add("Leverandør", 0)
[void]$listRight.Columns.Add("Sprog", 0)
$form.Controls.Add($listRight)

# Dynamic action button
$btnAction = New-Object System.Windows.Forms.Button
$btnAction.Text = "—"
$btnAction.Location = New-Object System.Drawing.Point(458, 202) # <-- var 190
$btnAction.Size = New-Object System.Drawing.Size(56, 120)
$btnAction.Font = New-Object System.Drawing.Font("Segoe UI", 8)
$btnAction.Enabled = $false
$form.Controls.Add($btnAction)

# Log (hidden by default)
$txtLog = New-Object System.Windows.Forms.RichTextBox
$txtLog.Location = New-Object System.Drawing.Point(12, 417) # <-- var 405
$txtLog.Size = New-Object System.Drawing.Size(948, 140)
$txtLog.ReadOnly = $true
$txtLog.Font = $font
$txtLog.Visible = $false
$form.Controls.Add($txtLog)

function Set-LogVisibility([bool]$show) {
    $txtLog.Visible = $show
    if ($show) { $form.Size = $SizeWithLog } else { $form.Size = $SizeNoLog }
}

function Set-TopStatus {
    if (Test-IsAdmin) {
        $lblTop.Text = "Tilføj eller fjern oplæsningsstemmer, så de kan bruges i Thorium Reader."
        #$lblTop.ForeColor = [System.Drawing.Color]::DarkGreen
    } else {
        $lblTop.Text = "Status: Ikke administrator. Du kan se stemmer, men kan ikke flytte/fjerne). Start programmet som Administrator."
        $lblTop.ForeColor = [System.Drawing.Color]::DarkRed
    }
}

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "Tilføj stemmer til Thorium"
$lblTitle.Location = New-Object System.Drawing.Point(12, 8)
$lblTitle.Size = New-Object System.Drawing.Size(500, 24)
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($lblTitle)

function Add-LeftRow($name, $vendor, $langLabel, $payload, [bool]$locked) {
    $item = New-Object System.Windows.Forms.ListViewItem($name)
    [void]$item.SubItems.Add($vendor)
    [void]$item.SubItems.Add($langLabel)
    $item.Tag = $payload
    if ($locked) { $item.ForeColor = [System.Drawing.Color]::Gray; $item.Checked = $false }
    [void]$listLeft.Items.Add($item)
}

function Add-RightRow($name, $vendor, $langLabel, $payload, [bool]$locked) {
    $item = New-Object System.Windows.Forms.ListViewItem($name)
    [void]$item.SubItems.Add($vendor)
    [void]$item.SubItems.Add($langLabel)
    $item.Tag = $payload
    if ($locked) { $item.ForeColor = [System.Drawing.Color]::Gray; $item.Checked = $false }
    [void]$listRight.Items.Add($item)
}

function Uncheck-All($lv) {
    foreach ($it in $lv.Items) { $it.Checked = $false }
}

function Get-CheckedCount($lv) {
    $c = 0
    foreach ($it in $lv.Items) { if ($it.Checked) { $c++ } }
    return $c
}

function Update-ActionButton {
    $leftChecked  = Get-CheckedCount $listLeft
    $rightChecked = Get-CheckedCount $listRight

    if ($leftChecked -gt 0 -and $rightChecked -gt 0) {
        $btnAction.Text = "—"
        $btnAction.Enabled = $false
        return
    }

    if ($leftChecked -gt 0) {
        $btnAction.Text = "Flyt`r`n→"
        $btnAction.Enabled = (Test-IsAdmin)
        $btnAction.Tag = "MOVE_IN"
        return
    }

    if ($rightChecked -gt 0) {
        $btnAction.Text = "←`r`nFjern"
        $btnAction.Enabled = (Test-IsAdmin)
        $btnAction.Tag = "MOVE_OUT"
        return
    }

    $btnAction.Text = "—"
    $btnAction.Enabled = $false
}

function Refresh-Lists {
    $listLeft.Items.Clear()
    $listRight.Items.Clear()

    $oneCore = Get-OneCoreVoices
    $sapi    = Get-SapiVoices

    if ($chkDanishOnly.Checked) {
        $sapi = $sapi | Where-Object { $_.LanguageHex -eq "0406" }
        $oneCore = $oneCore | Where-Object { $_.LanguageHex -eq "0406" }
		
    }

    $showMicrosoft = $chkShowMicrosoft.Checked

    # RIGHT: alle OneCore, men kun ThoriumBridge=1 kan markeres/fjernes
    foreach ($oc in ($oneCore | Sort-Object Vendor, Name, TokenName)) {
        if (-not $showMicrosoft -and (Is-MicrosoftVendor $oc.Vendor)) { continue }

        $isBridged = ($oc.ThoriumBridge -eq $BridgeMarkerValue)
        $locked = (-not $isBridged)

        $name = $oc.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = $oc.Display }
        $vendor = if ($oc.Vendor) { $oc.Vendor } else { "" }
        $langLabel = Get-LangLabel $oc.LanguageHex

        Add-RightRow -name $name -vendor $vendor -langLabel $langLabel -payload ([PSCustomObject]@{
            TokenName = $oc.TokenName
            CanRemove = $isBridged
        }) -locked:$locked
    }

    # LEFT: SAPI som ikke allerede findes som OneCore token
    foreach ($v in ($sapi | Sort-Object Vendor, Name, TokenName)) {
        $isMicrosoft = Is-MicrosoftVendor $v.Vendor
        if (-not $showMicrosoft -and $isMicrosoft) { continue }

        $ocExact = Find-OneCoreByTokenName -oneCoreVoices $oneCore -tokenName $v.TokenName
        if ($ocExact) { continue }

        $vendor = if ($v.Vendor) { $v.Vendor } else { "" }
        $langLabel = Get-LangLabel $v.LanguageHex

        # Microsoft SAPI vises kun hvis toggle, men låses altid
        $locked = $isMicrosoft

        Add-LeftRow -name $v.Name -vendor $vendor -langLabel $langLabel -payload ([PSCustomObject]@{
            TokenName = $v.TokenName
            Vendor    = $v.Vendor
            Locked    = $locked
        }) -locked:$locked
    }

    Log "Opdateret. Ikke i Thorium: $($listLeft.Items.Count) | I Thorium: $($listRight.Items.Count)"
    Update-ActionButton
	
	Set-EqualColumnWidths -ListView $listLeft
    Set-EqualColumnWidths -ListView $listRight
}

function Move-CheckedToThorium {
    if (-not (Test-IsAdmin)) {
        [System.Windows.Forms.MessageBox]::Show(
            "For at flytte/fjerne stemmer skal programmet køre som Administrator.",
            "Mangler rettigheder",
            "OK",
            "Warning"
        ) | Out-Null
        return
    }

    $targets = @()
    foreach ($it in $listLeft.Items) { if ($it.Checked) { $targets += $it } }
    if ($targets.Count -eq 0) { return }

    $count = 0
    foreach ($it in $targets) {
        $p = $it.Tag
        if (-not $p) { continue }
        if ($p.Locked) { continue }

        try { Install-VoiceToOneCore -tokenName $p.TokenName; $count++ }
        catch { Log "FEJL ved flyt til Thorium ($($p.TokenName)): $($_.Exception.Message)" }
    }

    Refresh-Lists
    if ($count -gt 0) {
        [System.Windows.Forms.MessageBox]::Show(
            "Stemmen er tilføjet til Thorium. Genstart Thorium for at bruge den nye stemme.",
            "Info",
            "OK",
            "Information"
        ) | Out-Null
    }
}

function Remove-CheckedFromThorium {
    if (-not (Test-IsAdmin)) {
        [System.Windows.Forms.MessageBox]::Show(
            "For at flytte/fjerne stemmer skal programmet køre som Administrator.",
            "Mangler rettigheder",
            "OK",
            "Warning"
        ) | Out-Null
        return
    }

    $targets = @()
    foreach ($it in $listRight.Items) { if ($it.Checked) { $targets += $it } }
    if ($targets.Count -eq 0) { return }

    $count = 0
    foreach ($it in $targets) {
        $p = $it.Tag
        if (-not $p) { continue }
        if (-not $p.CanRemove) { continue }

        try { if (Remove-FromThorium -tokenName $p.TokenName) { $count++ } }
        catch { Log "FEJL ved fjern fra Thorium ($($p.TokenName)): $($_.Exception.Message)" }
    }

    Refresh-Lists
    if ($count -gt 0) {
        [System.Windows.Forms.MessageBox]::Show(
            "Stemmen er fjernet fra Thorium. Det er nødvendigt at genstarte Thorium før ændringen træder i kraft.",
            "Info",
            "OK",
            "Information"
        ) | Out-Null
    }
}

# --- Events / protections ---

$listLeft.Add_ItemCheck({
    param($sender, $e)
    $item = $sender.Items[$e.Index]
    $p = $item.Tag
    if ($p -and $p.Locked -and $e.NewValue -eq [System.Windows.Forms.CheckState]::Checked) {
        $e.NewValue = [System.Windows.Forms.CheckState]::Unchecked
        [System.Windows.Forms.SystemSounds]::Beep.Play()
        return
    }
    if ($e.NewValue -eq [System.Windows.Forms.CheckState]::Checked) {
        Uncheck-All $listRight
    }
})

$listRight.Add_ItemCheck({
    param($sender, $e)
    $item = $sender.Items[$e.Index]
    $p = $item.Tag
    if ($p -and (-not $p.CanRemove) -and $e.NewValue -eq [System.Windows.Forms.CheckState]::Checked) {
        $e.NewValue = [System.Windows.Forms.CheckState]::Unchecked
        [System.Windows.Forms.SystemSounds]::Beep.Play()
        return
    }
    if ($e.NewValue -eq [System.Windows.Forms.CheckState]::Checked) {
        Uncheck-All $listLeft
    }
})

$listLeft.Add_ItemChecked({ Update-ActionButton })
$listRight.Add_ItemChecked({ Update-ActionButton })

$btnAction.Add_Click({
    if ($btnAction.Tag -eq "MOVE_IN") {
        Move-CheckedToThorium
    } elseif ($btnAction.Tag -eq "MOVE_OUT") {
        Remove-CheckedFromThorium
    }
})

$chkDanishOnly.Add_CheckedChanged({ Refresh-Lists })
$chkShowMicrosoft.Add_CheckedChanged({ Refresh-Lists })
$chkShowLog.Add_CheckedChanged({ Set-LogVisibility $chkShowLog.Checked })

$form.Add_KeyDown({
    param($sender, $e)
    if ($e.KeyCode -eq [System.Windows.Forms.Keys]::F5) {
        Set-TopStatus
        Refresh-Lists
        $e.Handled = $true
    }
})

$form.Add_Shown({
    Set-TopStatus
    Set-LogVisibility $false
    Refresh-Lists
    Log "Log er skjult som standard. Slå 'Vis log' til for fejlfinding."
    Log "Kun tokens markeret $BridgeMarkerName=1 kan fjernes."
})

[void]$form.ShowDialog()
