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
$Hashes = @{ "x86" = ""; "x64" = "" } # Diccionario para guardar los hashes

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

    # Copiar archivos
    $BinPath = Join-Path $ProjectDir "$ProjectName\bin\$Arch\Release"
    
    Write-Host "[-] Copiando desde: $BinPath" -ForegroundColor Gray
    if (Test-Path $BinPath) {
        Copy-Item "$BinPath\*" $DestArchFolder -Recurse -Exclude "*.pdb", "*.xml", "*.tmp" -Force
        
        # --- NUEVO: Calcular Hash MD5 del ejecutable ---
        $ExeFile = Join-Path $DestArchFolder "$ProjectName.exe"
        Write-Host "[-] Calculando MD5 para $ExeFile..." -ForegroundColor Gray
        $Hashes[$Arch] = (Get-FileHash $ExeFile -Algorithm MD5).Hash
        Write-Host "    Hash: $($Hashes[$Arch])" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] No se encontró la carpeta binaria esperada: $BinPath" -ForegroundColor Red
        exit
    }
}

# --- Empaquetado ---
Write-Header "PREPARANDO MANIFIESTOS"

# 1. Copiar extension.xml
$ManifestSource = Join-Path $ProjectDir "$ProjectName\extension.xml"
if (Test-Path $ManifestSource) {
    Copy-Item $ManifestSource $StagingFolder
}

# 2. Procesar e Inyectar Hashes en PadronWtd.ard
$ArdSource = Join-Path $ProjectDir "$ProjectName\PadronWtd.ard"
if (Test-Path $ArdSource) {
    Write-Host "[-] Procesando archivo .ard con nuevos hashes..." -ForegroundColor Yellow
    $ArdContent = Get-Content $ArdSource -Raw
    
    # Reemplazar los placeholders (o los hashes anteriores usando Regex si prefieres)
    # Aquí buscamos el atributo AddonSig dentro de cada bloque
    $ArdContent = $ArdContent -replace '(?<=<x86[^>]*AddonSig=")[^"]*', $Hashes["x86"]
    $ArdContent = $ArdContent -replace '(?<=<x64[^>]*AddonSig=")[^"]*', $Hashes["x64"]
    
    # Guardar el .ard modificado en la carpeta staging
    $ArdDest = Join-Path $StagingFolder "PadronWtd.ard"
    Set-Content -Path $ArdDest -Value $ArdContent
    Write-Host "[-] PadronWtd.ard actualizado e incluido en raíz." -ForegroundColor Green
} else {
    Write-Host "[WARNING] No se encontró PadronWtd.ard en la ruta del proyecto." -ForegroundColor Magenta
}

# --- Crear el ZIP final ---
$Timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$ZipName = "Addon_$($ProjectName)_$Timestamp.zip"
$ZipPath = Join-Path $BuildDir $ZipName

Write-Host "[-] Comprimiendo paquete..." -ForegroundColor Yellow
Compress-Archive -Path "$StagingFolder\*" -DestinationPath $ZipPath

Write-Host "`n¡EXITO! Paquete creado en: $ZipPath" -ForegroundColor Green
Write-Host "Recuerda: Si es una nueva versión, incrementa el número en el .ard y extension.xml" -ForegroundColor Cyan