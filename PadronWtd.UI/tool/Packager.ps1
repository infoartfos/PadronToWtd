param (
    [Parameter(Mandatory=$false)]
    [string]$ProjectDir = "C:\Users\cvalicenti\source\repos\PadronToWtd",

    [Parameter(Mandatory=$false)]
    [string]$BuildDir = "C:\Builds\AddonPackage",

    [Parameter(Mandatory=$false)]
    [string]$SolutionName = "PadronWtd.sln",

    [Parameter(Mandatory=$false)]
    [string]$ProjectName = "PadronWtd.UI",

    [Parameter(Mandatory=$false)]
    [string]$MSBuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)

function Write-Header ([string]$msg) {
    Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
    Write-Host " >> $msg" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
}

# --- Limpieza Inicial ---
if (Test-Path $BuildDir) { Remove-Item $BuildDir -Recurse -Force }
$StagingFolder = Join-Path $BuildDir "Staging"
New-Item -ItemType Directory -Path $StagingFolder | Out-Null

$SolutionPath = Join-Path $ProjectDir $SolutionName
$Architectures = @("x86", "x64")

foreach ($Arch in $Architectures) {
    Write-Header "PROCESANDO ARQUITECTURA: $Arch"
    
    $DestArchFolder = Join-Path $StagingFolder $Arch
    New-Item -ItemType Directory -Path $DestArchFolder | Out-Null

    Write-Host "[-] Compilando $Arch..." -ForegroundColor Yellow
    
    # Ejecutar MSBuild
    $msbuildArgs = @(
        "`"$SolutionPath`"",
        "/p:Configuration=Release",
        "/p:Platform=$Arch",
        "/t:Rebuild",
        "/p:DebugType=none",
        "/v:m",
        "/nologo"
    )
    
    & $MSBuildPath $msbuildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Falló la compilación de $Arch." -ForegroundColor Red
        exit
    }

    # Copiar archivos (Importante: la ruta debe coincidir con el csproj)
    $BinPath = Join-Path $ProjectDir "$ProjectName\bin\$Arch\Release"
    
    Write-Host "[-] Copiando desde: $BinPath" -ForegroundColor Gray
    if (Test-Path $BinPath) {
        Copy-Item "$BinPath\*" $DestArchFolder -Recurse -Exclude "*.pdb", "*.xml", "*.tmp" -Force
    } else {
        Write-Host "[ERROR] No se encontró la carpeta binaria esperada: $BinPath" -ForegroundColor Red
        exit
    }
}

# --- Empaquetado ---
Write-Header "GENERANDO ZIP FINAL"
$ManifestSource = Join-Path $ProjectDir "$ProjectName\extension.xml"
if (Test-Path $ManifestSource) {
    Copy-Item $ManifestSource $StagingFolder
}

$Timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$ZipPath = Join-Path $BuildDir "Addon_$($ProjectName)_$Timestamp.zip"


Write-Header "GENERANDO PAQUETE FINAL"

# Copiar extension.xml (si existe)
$ManifestSource = Join-Path $ProjectDir "$ProjectName\extension.xml"
if (Test-Path $ManifestSource) {
    Copy-Item $ManifestSource $StagingFolder
}

# COPIAR EL ARCHIVO .ARD (Nuevo paso)
$ArdSource = Join-Path $ProjectDir "$ProjectName\PadronWtd.ard"
if (Test-Path $ArdSource) {
    Write-Host "[-] Copiando archivo de registro .ard..." -ForegroundColor Yellow
    Copy-Item $ArdSource $StagingFolder
} else {
    Write-Host "[WARNING] No se encontró PadronWtd.ard en la raíz." -ForegroundColor Magenta
}

# Crear el ZIP
$Timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$ZipPath = Join-Path $BuildDir "Addon_$($ProjectName)_$Timestamp.zip"
Compress-Archive -Path "$StagingFolder\*" -DestinationPath $ZipPath

Write-Host "¡EXITO! Paquete creado en: $ZipPath" -ForegroundColor Green