using module .\StaticHostingProvider.psm1

class VercelHostingProvider : StaticHostingProvider {
    [string]$ConfigurationPath

    VercelHostingProvider([string]$configurationPath) {
        $this.ConfigurationPath = $configurationPath
        $this.MaximumBytes = 100000000
    }

    [System.Collections.IDictionary] ConfigurationFiles() {
        return @{'vercel.json' = [IO.File]::ReadAllText($this.ConfigurationPath)}
    }
}
