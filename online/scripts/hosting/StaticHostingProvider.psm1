class StaticHostingProvider {
    [long]$MaximumBytes = [long]::MaxValue

    [System.Collections.IDictionary] ConfigurationFiles() {
        return @{}
    }

    [string] TargetPath([string]$root, [string]$relative) {
        if ([string]::IsNullOrWhiteSpace($relative) -or
            $relative -match '(^|[/\\])\.\.([/\\]|$)|:' -or [IO.Path]::IsPathRooted($relative)) {
            throw 'Static export paths must be relative and cannot traverse directories.'
        }
        $target = [IO.Path]::GetFullPath((Join-Path $root $relative.Replace('/', '\')))
        if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Static export path escapes the destination.'
        }
        return $target
    }

    [string] Export([System.Collections.IDictionary]$files,
        [System.Collections.IDictionary]$generated, [string]$destination) {
        $root = [IO.Path]::GetFullPath($destination).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $root) { throw 'Static export destination already exists; no files were overwritten.' }
        $copies = [ordered]@{}
        $text = [ordered]@{}
        $utf8 = [Text.UTF8Encoding]::new($false)
        [long]$bytes = 0
        foreach ($entry in $files.GetEnumerator()) {
            $target = $this.TargetPath($root, 'public\' + $entry.Key)
            if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) { throw 'A static build input is missing.' }
            $bytes += (Get-Item -LiteralPath $entry.Value).Length
            if ($copies.Contains($target)) { throw 'Duplicate static export path.' }
            $copies[$target] = $entry.Value
        }
        foreach ($entry in $generated.GetEnumerator()) {
            $target = $this.TargetPath($root, 'public\' + $entry.Key)
            if ($copies.Contains($target) -or $text.Contains($target)) { throw 'Duplicate static export path.' }
            $text[$target] = [string]$entry.Value
        }
        foreach ($entry in $this.ConfigurationFiles().GetEnumerator()) {
            $target = $this.TargetPath($root, $entry.Key)
            if ($copies.Contains($target) -or $text.Contains($target)) { throw 'Duplicate provider configuration path.' }
            $text[$target] = [string]$entry.Value
        }
        foreach ($value in $text.Values) { $bytes += $utf8.GetByteCount($value) }
        if ($bytes -gt $this.MaximumBytes) { throw 'Static export exceeds the configured hosting upload limit.' }
        $null = [IO.Directory]::CreateDirectory($root)
        foreach ($entry in $copies.GetEnumerator()) {
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key))
            [IO.File]::Copy($entry.Value, $entry.Key, $false)
        }
        foreach ($entry in $text.GetEnumerator()) {
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key))
            [IO.File]::WriteAllText($entry.Key, $entry.Value, $utf8)
        }
        return $root
    }
}
