function Make-Ico {
    param([string]$Path, [byte]$R, [byte]$G, [byte]$B)
    $dataOffset = 22  # 6 + 16
    $bmp = New-Object System.Collections.Generic.List[byte]
    # BITMAPINFOHEADER 40 bytes
    $bmp.AddRange([byte[]](40,0,0,0))   # biSize
    $bmp.AddRange([byte[]](16,0,0,0))   # biWidth = 16
    $bmp.AddRange([byte[]](32,0,0,0))   # biHeight = 32 (2x for ICO)
    $bmp.AddRange([byte[]](1,0))         # biPlanes
    $bmp.AddRange([byte[]](32,0))        # biBitCount
    $bmp.AddRange([byte[]](0,0,0,0))     # biCompression
    $bmp.AddRange([byte[]](0,4,0,0))     # biSizeImage 16*16*4=1024
    $bmp.AddRange([byte[]](0,0,0,0))     # biXPelsPerMeter
    $bmp.AddRange([byte[]](0,0,0,0))     # biYPelsPerMeter
    $bmp.AddRange([byte[]](0,0,0,0))     # biClrUsed
    $bmp.AddRange([byte[]](0,0,0,0))     # biClrImportant
    # Pixel data BGRA, bottom-to-top
    for($row=15;$row -ge 0;$row--){
        for($col=0;$col -lt 16;$col++){
            $d=[math]::Sqrt(($col-7.5)*($col-7.5)+($row-7.5)*($row-7.5))
            if($d -le 6.0){
                $bmp.Add($B); $bmp.Add($G); $bmp.Add($R); $bmp.Add(255)
            } else {
                $bmp.AddRange([byte[]](0,0,0,0))
            }
        }
    }
    # AND mask (16x16, 1bpp padded to 4-byte rows) = 16*2 = 32 bytes
    for($i=0;$i -lt 32;$i++){$bmp.Add(0)}

    $bd = $bmp.ToArray()
    $sz  = [BitConverter]::GetBytes([uint32]$bd.Length)
    $off = [BitConverter]::GetBytes([uint32]$dataOffset)

    $ico = New-Object System.Collections.Generic.List[byte]
    # ICO header: Reserved(2) + Type=1(2) + Count=1(2)
    $ico.AddRange([byte[]](0,0, 1,0, 1,0))
    # Directory entry: Width(1)+Height(1)+ColorCnt(1)+Res(1)+Planes(2)+BitCnt(2)+Size(4)+Offset(4) = 16 bytes
    $ico.Add(16)     # width
    $ico.Add(16)     # height
    $ico.Add(0)      # color count
    $ico.Add(0)      # reserved
    $ico.AddRange([byte[]](1,0))   # planes
    $ico.AddRange([byte[]](32,0))  # bit count
    $ico.AddRange($sz)
    $ico.AddRange($off)
    # Image data
    $ico.AddRange($bd)

    [IO.File]::WriteAllBytes($Path, $ico.ToArray())
    Write-Host "Created: $Path ($($ico.Count) bytes)"
}

$resDir = "$PSScriptRoot\src\PokeyApp\Resources"

Make-Ico "$resDir\tray-connected.ico"    68  255 136
Make-Ico "$resDir\tray-disconnected.ico" 255 68  68
Write-Host "Done."
