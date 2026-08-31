param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$kimiHome = Join-Path $HOME '.kimi-code'
$claudeHome = Join-Path $HOME '.claude'

function Mirror($src, $dst) {
    if ($DryRun) { robocopy $src $dst /MIR /L /NFL /NDL /NJH }
    else { robocopy $src $dst /MIR /NFL /NDL /NJH }
    if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }
}

function Copy-One($src, $dstDir) {
    if ($DryRun) { Write-Host "Would copy $src -> $dstDir" }
    else { Copy-Item $src $dstDir -Force }
}

Mirror (Join-Path $root '.kimi-code/agents') (Join-Path $kimiHome 'agents')
Mirror (Join-Path $root '.kimi-code/skills') (Join-Path $kimiHome 'skills')
Mirror (Join-Path $root 'TASKS/_TEMPLATE')  (Join-Path $kimiHome 'TASKS/_TEMPLATE')
Copy-One (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $kimiHome
Copy-One (Join-Path $root '.kimi-code/SKILLFLOW.md') $kimiHome
Copy-One (Join-Path $root '.kimi-code/config.toml') $kimiHome
Copy-One (Join-Path $root '.kimi-code/AGENTS.md') $kimiHome

Mirror (Join-Path $root '.claude/agents') (Join-Path $claudeHome 'agents')
Mirror (Join-Path $root '.claude/skills') (Join-Path $claudeHome 'skills')
Mirror (Join-Path $root 'TASKS/_TEMPLATE') (Join-Path $claudeHome 'TASKS/_TEMPLATE')
Copy-One (Join-Path $root '.claude/CLAUDE.md') $claudeHome
Copy-One (Join-Path $root '.claude/SKILLFLOW.md') $claudeHome
Copy-One (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $claudeHome

if ($DryRun) { Write-Host 'Dry run complete - no changes made.' }
exit 0
