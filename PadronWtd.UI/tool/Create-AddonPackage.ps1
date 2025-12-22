# --- CONFIGURACIÓN ---
$projectName = "PadronWtd.UI"
$solutionPath = "C:\Users\cvalicenti\source\repos\PadronToWtd\PadronToWtd.sln"
$msBuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" # Ajusta según tu versión de VS
$outputFolder = "C:\Builds\AddonPackage"
$stagingFolder = "$outputFolder\Staging"

# Limpiar carpetas previas
if (Test-Path $outputFolder) { Remove-Item $outputFolder -Recurse -Force }
New-Item -ItemType Directory -Path $stagingFolder
New-Item -ItemType Directory -Path "$stagingFolder\x86"
New-Item -ItemType Directory -Path "$stagingFolder\x64"

# --- 1. COMPILAR EN 32 BITS (x86) ---
Write-Host "Compilando x86..." -ForegroundColor Cyan
& $msBuildPath $solutionPath /p:Configuration=Release /p:Platform=x86 /t:Rebuild

# Copiar archivos x86 (Ajusta la ruta de bin según tu proyecto)
$binX86 = "C:\Users\cvalicenti\source\repos\PadronToWtd\$projectName\bin\x86\Release"
Copy-Item "$binX86\*" "$stagingFolder\x86" -Recurse

# --- 2. COMPILAR EN 64 BITS (x64) ---
Write-Host "Compilando x64..." -ForegroundColor Cyan
& $msBuildPath $solutionPath /p:Configuration=Release /p:Platform=x64 /t:Rebuild

# Copiar archivos x64
$binX64 = "C:\Users\cvalicenti\source\repos\PadronToWtd\$projectName\bin\x64\Release"
Copy-Item "$binX64\*" "$stagingFolder\x64" -Recurse

# --- 3. COPIAR EL MANIFIESTO (extension.xml) ---
# Debes tener un archivo extension.xml ya creado en la raíz de tu proyecto
Copy-Item "C:\Users\cvalicenti\source\repos\PadronToWtd\extension.xml" "$stagingFolder\"

# --- 4. CREAR EL ZIP ---
Write-Host "Creando archivo ZIP para SAP..." -ForegroundColor Green
$zipName = "$outputFolder\PadronWtd_Installer_$(Get-Date -Format 'yyyyMMdd').zip"
Compress-Archive -Path "$stagingFolder\*" -DestinationPath $zipName

Write-Host "¡Proceso terminado! Instalador generado en: $zipName" -ForegroundColor Yellow