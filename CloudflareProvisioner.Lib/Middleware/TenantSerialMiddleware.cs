using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
//using System.Web.Http;

namespace CloudflareProvisioner.Lib.Middleware
{
    public class TenantSerialMiddleware : DelegatingHandler
    {
        private readonly string _localSerial;

        public TenantSerialMiddleware(string localSerial)
        {
            _localSerial = localSerial ?? throw new ArgumentNullException(nameof(localSerial));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Permitir endpoints públicos
            if (request.RequestUri.AbsolutePath.Contains("/api/health") ||
                request.RequestUri.AbsolutePath.Contains("/api/provisioning/enroll"))
            {
                return await base.SendAsync(request, cancellationToken);
            }

#if DEBUG
            return await base.SendAsync(request, cancellationToken);
#endif

            //// Extrair hostname do request
            //var host = request.RequestUri.Host;
            //if (string.IsNullOrEmpty(host))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.BadRequest,
            //        "Hostname não encontrado no request."
            //    );
            //}

            //// Extrair serial do hostname (ex: 87432.easy4all.net -> 87432)
            //var hostParts = host.Split('.');
            //if (hostParts.Length < 2)
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.BadRequest,
            //        "Hostname inválido. Formato esperado: <serial>.<domain>"
            //    );
            //}

            //var tenantSerial = hostParts[0];

            //// Validar que o serial contém apenas dígitos
            //if (!System.Text.RegularExpressions.Regex.IsMatch(tenantSerial, @"^\d+$"))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.BadRequest,
            //        "Serial do hostname inválido. Deve conter apenas dígitos."
            //    );
            //}

            //// Isolamento "hard": esta máquina só responde pelo seu serial
            //if (!string.Equals(tenantSerial, _localSerial, StringComparison.OrdinalIgnoreCase))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.NotFound,
            //        $"Tenant '{tenantSerial}' não encontrado nesta instância. Esta instância serve apenas o serial '{_localSerial}'."
            //    );
            //}

            // Adicionar serial ao request para uso posterior
            //request.Properties["TenantSerial"] = tenantSerial;

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
