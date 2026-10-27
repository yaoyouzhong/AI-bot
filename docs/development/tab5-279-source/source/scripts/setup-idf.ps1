. "$PSScriptRoot\IdfEnvironment.ps1"
if (-not (Test-Path "$env:IDF_PATH\tools\idf_tools.py")) { throw 'Clone ESP-IDF v5.4.2 recursively into .tools/esp-idf first.' }
python "$env:IDF_PATH\tools\idf_tools.py" install --targets=esp32p4
if ($LASTEXITCODE) { throw 'IDF tool installation failed' }
python "$env:IDF_PATH\tools\idf_tools.py" install-python-env
if ($LASTEXITCODE) { throw 'IDF Python environment installation failed' }
