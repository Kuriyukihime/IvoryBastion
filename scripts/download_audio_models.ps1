param(
    [ValidateSet('all','laughter','screening')][string]$Model = 'all',
    [string]$HfCli = '',
    [switch]$HttpOnly
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $HfCli) {
    $HfCli = Join-Path $root '../TheDiva/.build/voice-env/Scripts/hf.exe'
}
if (-not (Test-Path -LiteralPath $HfCli)) { throw 'Supply -HfCli with an installed Hugging Face CLI.' }
# User preference: eight concurrent download batches for all HF models.
$env:HF_XET_NUM_CONCURRENT_RANGE_GETS = '8'
$env:HF_HUB_DOWNLOAD_TIMEOUT = '45'
$env:HF_HUB_DISABLE_XET = $(if ($HttpOnly) { '1' } else { '0' })
$jobs = @()
if ($Model -in @('all','laughter')) {
    $jobs += @{
        Repo = 'ResembleAI/chatterbox-turbo'
        Folder = '.build/chatterbox-turbo'
        Files = @('t3_turbo_v1.safetensors','s3gen_meanflow.safetensors','ve.safetensors','conds.pt','tokenizer_config.json','special_tokens_map.json','added_tokens.json','vocab.json','merges.txt','README.md')
    }
}
if ($Model -in @('all','screening')) {
    $jobs += @{
        Repo = 'MIT/ast-finetuned-audioset-10-10-0.4593'
        Folder = '.build/audio-event-model'
        Files = @('model.safetensors','config.json','preprocessor_config.json','README.md')
    }
}
foreach ($job in $jobs) {
    $destination = Join-Path $root $job.Folder
    & $HfCli download $job.Repo @($job.Files) --local-dir $destination --max-workers 8
    if ($LASTEXITCODE -ne 0) { throw ('Model download failed: ' + $job.Repo) }
}
