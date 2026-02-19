using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CloudflareProvisioner.Lib.Services
{
    public class CloudflaredService
    {
        private readonly string _cloudflaredPath;
        private readonly string _configPath;
        private Process _cloudflaredProcess;

        public CloudflaredService()
        {
            _cloudflaredPath = @"C:\Program Files\cloudflared\cloudflared.exe" ?? @"C:\Program Files\cloudflared\cloudflared.exe";
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cloudflared", "config.yml");
        }

        /// <summary>
        /// Verifica se o cloudflared está instalado
        /// </summary>
        public bool IsInstalled()
        {
            return File.Exists(_cloudflaredPath);
        }

        /// <summary>
        /// Instala o cloudflared como serviço Windows
        /// </summary>
        public async Task<bool> InstallServiceAsync(string tunnelToken)
        {
            if (!IsInstalled())
            {
                throw new Exception("cloudflared não está instalado. Por favor, instale primeiro.");
            }

            try
            {
                // Para Named Tunnels, o cloudflared service install aceita o token diretamente
                // Formato: cloudflared service install <token>
                // O token é o connection token retornado pela API do Cloudflare
                
                var installArgs = $"service install {tunnelToken}";
                var installResult = await RunCloudflaredAsync(installArgs);
                
                if (!installResult.Success)
                {
                    throw new Exception($"Erro ao instalar serviço: {installResult.Error}");
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao instalar cloudflared: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Inicia o serviço cloudflared
        /// </summary>
        public async Task<bool> StartServiceAsync()
        {
            try
            {
                var startArgs = "service start";
                var result = await RunCloudflaredAsync(startArgs);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Para o serviço cloudflared
        /// </summary>
        public async Task<bool> StopServiceAsync()
        {
            try
            {
                var stopArgs = "service stop";
                var result = await RunCloudflaredAsync(stopArgs);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Desinstala o serviço cloudflared
        /// </summary>
        public async Task<bool> UninstallServiceAsync()
        {
            try
            {
                var uninstallArgs = "service uninstall";
                var result = await RunCloudflaredAsync(uninstallArgs);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executa o cloudflared com os argumentos fornecidos
        /// </summary>
        private async Task<(bool Success, string Output, string Error)> RunCloudflaredAsync(string arguments)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _cloudflaredPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        return (false, "", "Não foi possível iniciar o processo cloudflared");
                    }

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();

                    return (process.ExitCode == 0, output, error);
                }
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Verifica o status do serviço
        /// </summary>
        public bool IsServiceRunning()
        {
            try
            {
                var process = Process.GetProcessesByName("cloudflared");
                return process.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
