[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$names = @(
    'eveo-gdi-safety',
    'eveo-ci-validation',
    'eveo-multibox-6plus1',
    'eveo-upstream-research',
    'eveo-pr-review'
)
$problems = [System.Collections.Generic.List[string]]::new()
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)

foreach ($name in $names) {
    $paths = @(
        (Join-Path $RepositoryRoot ".agents/skills/$name/SKILL.md"),
        (Join-Path $RepositoryRoot ".claude/skills/$name/SKILL.md")
    )
    $complete = $true
    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $problems.Add("Missing file: $path")
            $complete = $false
            continue
        }
        try {
            $content = $utf8.GetString([System.IO.File]::ReadAllBytes($path))
            # Deliberately restricted YAML: two portable, single-line scalars.
            # Reject other YAML constructs rather than pretend to parse them.
            $pattern = "\A---\r?\nname: (?<name>[a-z0-9-]+)\r?\ndescription: '(?<description>(?:[^'\r\n]|'')+)'\r?\n---\r?\n(?<body>[\s\S]+)\z"
            $match = [regex]::Match($content, $pattern)
            if (-not $match.Success) {
                $problems.Add("Invalid restricted YAML/frontmatter or empty body: $path")
            } elseif ($match.Groups['name'].Value -cne $name) {
                $problems.Add("Name differs from directory: $path")
            } elseif ([string]::IsNullOrWhiteSpace($match.Groups['body'].Value)) {
                $problems.Add("Empty body: $path")
            }
        } catch {
            $problems.Add("Cannot read valid UTF-8: $path ($($_.Exception.Message))")
        }
    }
    if ($complete) {
        $left = [System.IO.File]::ReadAllBytes($paths[0])
        $right = [System.IO.File]::ReadAllBytes($paths[1])
        if ([Convert]::ToBase64String($left) -cne [Convert]::ToBase64String($right)) {
            $problems.Add("Byte mismatch: $name")
        }
    }
}

if ($problems.Count -gt 0) {
    foreach ($problem in $problems) { [Console]::Error.WriteLine($problem) }
    exit 1
}
Write-Output 'PASS: 5 skills, 10 files, valid restricted YAML, identical bytes in each pair.'
exit 0
