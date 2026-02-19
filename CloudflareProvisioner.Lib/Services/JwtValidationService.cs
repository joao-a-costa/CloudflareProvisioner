using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace CloudflareProvisioner.Lib.Services
{
    public class JwtValidationService
    {
        private readonly string _teamName;
        private readonly string _accessAud;
        private readonly HttpClient _httpClient;
        private SecurityKey[] _cachedKeys;
        private DateTime _keysExpiry;
        private readonly object _lockObject = new object();
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(10);

        public JwtValidationService(string teamName, string accessAud)
        {
            _teamName = teamName;
            _accessAud = accessAud;
            _httpClient = new HttpClient();
            _keysExpiry = DateTime.MinValue;
        }

        /// <summary>
        /// Valida o JWT do Cloudflare Access (Cf-Access-Jwt-Assertion)
        /// </summary>
        public bool ValidateJwt(string jwtToken, out JwtSecurityToken validatedToken)
        {
            validatedToken = null;

            if (string.IsNullOrEmpty(jwtToken))
            {
                return false;
            }

            try
            {
                var keys = GetSigningKeys().GetAwaiter().GetResult();
                if (keys == null || keys.Length == 0)
                {
                    return false;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{_teamName}.cloudflareaccess.com",
                    // Se AccessAud estiver vazio, não validar audience (para testes)
                    ValidateAudience = !string.IsNullOrEmpty(_accessAud),
                    ValidAudience = _accessAud,
                    ValidateLifetime = true,
                    RequireSignedTokens = true,
                    IssuerSigningKeys = keys
                };

                var principal = tokenHandler.ValidateToken(jwtToken, validationParameters, out SecurityToken validatedSecurityToken);
                validatedToken = validatedSecurityToken as JwtSecurityToken;

                return validatedToken != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Obtém as chaves de assinatura do Cloudflare Access (JWKS)
        /// </summary>
        private async Task<SecurityKey[]> GetSigningKeys()
        {
            lock (_lockObject)
            {
                // Verificar cache
                if (_cachedKeys != null && DateTime.UtcNow < _keysExpiry)
                {
                    return _cachedKeys;
                }
            }

            try
            {
                var jwksUrl = $"https://{_teamName}.cloudflareaccess.com/cdn-cgi/access/certs";
                var response = await _httpClient.GetStringAsync(jwksUrl);

                var json = JObject.Parse(response);
                var keysArray = json["keys"] as JArray;

                if (keysArray == null)
                {
                    return Array.Empty<SecurityKey>();
                }

                var keys = new List<SecurityKey>();
                foreach (var keyJson in keysArray)
                {
                    try
                    {
                        var jwkJson = keyJson.ToString();
                        var key = new JsonWebKey(jwkJson);
                        if (key != null && !string.IsNullOrEmpty(key.Kty))
                        {
                            keys.Add(key);
                        }
                    }
                    catch
                    {
                        // Ignorar chaves inválidas
                    }
                }

                lock (_lockObject)
                {
                    _cachedKeys = keys.ToArray();
                    _keysExpiry = DateTime.UtcNow.Add(_cacheTimeout);
                }

                return _cachedKeys;
            }
            catch (Exception)
            {
                // Em caso de erro, retornar cache antigo se existir
                lock (_lockObject)
                {
                    return _cachedKeys ?? Array.Empty<SecurityKey>();
                }
            }
        }

        /// <summary>
        /// Extrai claims do JWT validado
        /// </summary>
        public Dictionary<string, string> ExtractClaims(JwtSecurityToken token)
        {
            var claims = new Dictionary<string, string>();
            if (token != null)
            {
                foreach (var claim in token.Claims)
                {
                    claims[claim.Type] = claim.Value;
                }
            }
            return claims;
        }
    }
}
