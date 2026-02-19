using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudflareProvisioner.Lib.Services;

//using System.Web.Http;

namespace CloudflareProvisioner.Lib.Middleware
{
    public class CloudflareAccessJwtMiddleware : DelegatingHandler
    {
        private readonly JwtValidationService _jwtValidationService;
        private readonly string _teamName;
        private readonly string _accessAud;

        public CloudflareAccessJwtMiddleware(string teamName, string accessAud)
        {
            _teamName = teamName;
            _accessAud = accessAud;
            _jwtValidationService = new JwtValidationService(teamName, accessAud);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Permitir endpoints públicos (ex: health check)
            if (request.RequestUri.AbsolutePath.Contains("/api/health") || 
                request.RequestUri.AbsolutePath.Contains("/api/provisioning/enroll") ||
                request.RequestUri.AbsolutePath.Contains("/api/provisioning/status"))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // Se AccessAud não estiver configurado, permitir acesso sem validação JWT (modo de teste)
            if (string.IsNullOrEmpty(_accessAud))
            {
                // Em produção, remover este bloco ou lançar exceção
                // Por enquanto, permite acesso sem validação para testes
                return await base.SendAsync(request, cancellationToken);
            }
            else
            {
#if DEBUG
                return await base.SendAsync(request, cancellationToken);
#endif
            }

            //// Verificar se o header Cf-Access-Jwt-Assertion está presente
            //if (!request.Headers.Contains("Cf-Access-Jwt-Assertion"))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.Unauthorized,
            //        "Header Cf-Access-Jwt-Assertion não encontrado. Acesso negado."
            //    );
            //}

            //var jwtToken = request.Headers.GetValues("Cf-Access-Jwt-Assertion")?.FirstOrDefault();
            
            //if (string.IsNullOrEmpty(jwtToken))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.Unauthorized,
            //        "Token JWT inválido ou vazio."
            //    );
            //}

            //// Validar o JWT
            //if (!_jwtValidationService.ValidateJwt(jwtToken, out var validatedToken))
            //{
            //    return request.CreateErrorResponse(
            //        HttpStatusCode.Unauthorized,
            //        "Token JWT inválido ou expirado."
            //    );
            //}

            //// Adicionar claims ao request para uso posterior
            //var claims = _jwtValidationService.ExtractClaims(validatedToken);
            //request.Properties["CloudflareClaims"] = claims;
            //request.Properties["CloudflareSubject"] = claims.ContainsKey("sub") ? claims["sub"] : null;

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
