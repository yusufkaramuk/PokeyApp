# 440 Hz, 0.3 saniye, 44100 Hz, 16-bit mono PCM WAV oluştur
$sampleRate = 44100
$duration   = 0.3
$freq       = 880.0   # La5 tonu
$amplitude  = 20000

$numSamples = [int]($sampleRate * $duration)
$wav = New-Object System.Collections.Generic.List[byte]

function Write-LE-Int32 { param([int]$v) $wav.AddRange([BitConverter]::GetBytes([int32]$v)) }
function Write-LE-Int16 { param([int16]$v) $wav.AddRange([BitConverter]::GetBytes($v)) }
function Write-LE-UInt32 { param([uint32]$v) $wav.AddRange([BitConverter]::GetBytes($v)) }
function Write-LE-UInt16 { param([uint16]$v) $wav.AddRange([BitConverter]::GetBytes($v)) }
function Write-FourCC { param([string]$s) $wav.AddRange([System.Text.Encoding]::ASCII.GetBytes($s)) }

$dataSize = $numSamples * 2  # 16-bit = 2 bytes/sample

# RIFF header
Write-FourCC "RIFF"
Write-LE-UInt32 ($dataSize + 36)
Write-FourCC "WAVE"

# fmt  chunk
Write-FourCC "fmt "
Write-LE-UInt32 16           # chunk size
Write-LE-UInt16 1            # PCM
Write-LE-UInt16 1            # mono
Write-LE-UInt32 $sampleRate
Write-LE-UInt32 ($sampleRate * 2)  # byte rate
Write-LE-UInt16 2            # block align
Write-LE-UInt16 16           # bits per sample

# data chunk
Write-FourCC "data"
Write-LE-UInt32 $dataSize

# Samples: fade-in/fade-out envelope
for($i=0;$i -lt $numSamples;$i++){
    $t = $i / $sampleRate
    # Sine wave with linear fade-in (20ms) and fade-out (50ms)
    $env = 1.0
    if($t -lt 0.02) { $env = $t / 0.02 }
    elseif($t -gt ($duration - 0.05)) { $env = ($duration - $t) / 0.05 }
    $sample = [int]($amplitude * $env * [math]::Sin(2 * [math]::PI * $freq * $t))
    $sample = [math]::Max(-32768, [math]::Min(32767, $sample))
    Write-LE-Int16 ([int16]$sample)
}

$outPath = "$PSScriptRoot\src\PokeyApp\Resources\poke.wav"
[IO.File]::WriteAllBytes($outPath, $wav.ToArray())
Write-Host "WAV oluşturuldu: $outPath ($($wav.Count) bytes)"
