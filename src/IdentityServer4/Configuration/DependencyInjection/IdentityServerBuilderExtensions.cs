﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityModel;
using IdentityServer4;
using IdentityServer4.Configuration;
using IdentityServer4.Configuration.DependencyInjection;
using IdentityServer4.Endpoints;
using IdentityServer4.Events;
using IdentityServer4.Hosting;
using IdentityServer4.Models;
using IdentityServer4.ResponseHandling;
using IdentityServer4.Services;
using IdentityServer4.Services.Default;
using IdentityServer4.Services.InMemory;
using IdentityServer4.Stores;
using IdentityServer4.Stores.InMemory;
using IdentityServer4.Stores.Serialization;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CryptoRandom = IdentityModel.CryptoRandom;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdentityServerBuilderExtensions
    {
        public static IIdentityServerBuilder AddRequiredPlatformServices(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.AddAuthentication();
            builder.Services.AddOptions();

            builder.Services.AddSingleton(
                resolver => resolver.GetRequiredService<IOptions<IdentityServerOptions>>().Value);

            return builder;
        }

        public static IIdentityServerBuilder AddDefaultEndpoints(this IIdentityServerBuilder builder)
        {
            builder.Services.AddSingleton<IEndpointRouter>(resolver =>
            {
                return new EndpointRouter(Constants.EndpointPathToNameMap,
                    resolver.GetRequiredService<IdentityServerOptions>(),
                    resolver.GetServices<EndpointMapping>(),
                    resolver.GetRequiredService<ILogger<EndpointRouter>>());
            });

            builder.AddEndpoint<AuthorizeEndpoint>(EndpointName.Authorize);
            builder.AddEndpoint<CheckSessionEndpoint>(EndpointName.CheckSession);
            builder.AddEndpoint<DiscoveryEndpoint>(EndpointName.Discovery);
            builder.AddEndpoint<EndSessionEndpoint>(EndpointName.EndSession);
            builder.AddEndpoint<IntrospectionEndpoint>(EndpointName.Introspection);
            builder.AddEndpoint<RevocationEndpoint>(EndpointName.Revocation);
            builder.AddEndpoint<TokenEndpoint>(EndpointName.Token);
            builder.AddEndpoint<UserInfoEndpoint>(EndpointName.UserInfo);

            return builder;
        }

        public static IIdentityServerBuilder AddEndpoint<T>(this IIdentityServerBuilder builder, EndpointName endpoint)
            where T : class, IEndpoint
        {
            builder.Services.AddTransient<T>();
            builder.Services.AddSingleton(new EndpointMapping { Endpoint = endpoint, Handler = typeof(T) });

            return builder;
        }

        public static IIdentityServerBuilder AddCoreServices(this IIdentityServerBuilder builder)
        {
            builder.Services.AddTransient<ScopeSecretValidator>();
            builder.Services.AddTransient<SecretParser>();
            builder.Services.AddTransient<ClientSecretValidator>();
            builder.Services.AddTransient<SecretValidator>();
            builder.Services.AddTransient<ScopeValidator>();
            builder.Services.AddTransient<ExtensionGrantValidator>();
            builder.Services.AddTransient<BearerTokenUsageValidator>();
            builder.Services.AddTransient<PersistentGrantSerializer>();
            builder.Services.AddTransient<EventServiceHelper>();

            builder.Services.AddTransient<ISessionIdService, DefaultSessionIdService>();
            builder.Services.AddTransient<IClientSessionService, DefaultClientSessionService>();
            builder.Services.AddTransient(typeof(MessageCookie<>));
            builder.Services.AddScoped<AuthenticationHandler>();

            builder.Services.AddCors();
            builder.Services.AddTransientDecorator<ICorsPolicyProvider, CorsPolicyProvider>();

            return builder;
        }

        public static IIdentityServerBuilder AddPluggableServices(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddTransient<IPersistedGrantService, DefaultPersistedGrantService>();
            builder.Services.TryAddTransient<IKeyMaterialService, DefaultKeyMaterialService>();
            builder.Services.TryAddTransient<IEventService, DefaultEventService>();
            builder.Services.TryAddTransient<ITokenService, DefaultTokenService>();
            builder.Services.TryAddTransient<ITokenCreationService, DefaultTokenCreationService>();
            builder.Services.TryAddTransient<IClaimsService, DefaultClaimsService>();
            builder.Services.TryAddTransient<IRefreshTokenService, DefaultRefreshTokenService>();
            builder.Services.TryAddTransient<IConsentService, DefaultConsentService>();
            builder.Services.TryAddTransient<ICorsPolicyService, DefaultCorsPolicyService>();
            builder.Services.TryAddTransient<IProfileService, DefaultProfileService>();
            builder.Services.TryAddTransient(typeof(IMessageStore<>), typeof(CookieMessageStore<>));
            builder.Services.TryAddTransient<IIdentityServerInteractionService, DefaultIdentityServerInteractionService>();

            return builder;
        }

        public static IIdentityServerBuilder AddValidators(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddTransient<IEndSessionRequestValidator, EndSessionRequestValidator>();
            builder.Services.TryAddTransient<ITokenRevocationRequestValidator, TokenRevocationRequestValidator>();
            builder.Services.TryAddTransient<IAuthorizeRequestValidator, AuthorizeRequestValidator>();
            builder.Services.TryAddTransient<ITokenRequestValidator, TokenRequestValidator>();
            builder.Services.TryAddTransient<IRedirectUriValidator, StrictRedirectUriValidator>();
            builder.Services.TryAddTransient<ITokenValidator, TokenValidator>();
            builder.Services.TryAddTransient<IIntrospectionRequestValidator, IntrospectionRequestValidator>();
            builder.Services.TryAddTransient<IResourceOwnerPasswordValidator, NotSupportedResouceOwnerPasswordValidator>();
            builder.Services.TryAddTransient<ICustomTokenValidator, DefaultCustomTokenValidator>();
            builder.Services.TryAddTransient<ICustomAuthorizeRequestValidator, DefaultCustomAuthorizeRequestValidator>();
            builder.Services.TryAddTransient<ICustomTokenRequestValidator, DefaultCustomTokenRequestValidator>();

            return builder;
        }

        public static IIdentityServerBuilder AddResponseGenerators(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddTransient<ITokenResponseGenerator, TokenResponseGenerator>();
            builder.Services.TryAddTransient<IUserInfoResponseGenerator, UserInfoResponseGenerator>();
            builder.Services.TryAddTransient<IIntrospectionResponseGenerator, IntrospectionResponseGenerator>();
            builder.Services.TryAddTransient<IAuthorizeInteractionResponseGenerator, AuthorizeInteractionResponseGenerator>();
            builder.Services.TryAddTransient<IAuthorizeResponseGenerator, AuthorizeResponseGenerator>();

            return builder;
        }

        public static IIdentityServerBuilder AddDefaultSecretParsers(this IIdentityServerBuilder builder)
        {
            builder.Services.AddTransient<ISecretParser, BasicAuthenticationSecretParser>();
            builder.Services.AddTransient<ISecretParser, PostBodySecretParser>();

            return builder;
        }

        public static IIdentityServerBuilder AddDefaultSecretValidators(this IIdentityServerBuilder builder)
        {
            builder.Services.AddTransient<ISecretValidator, HashedSharedSecretValidator>();

            return builder;
        }

        public static IIdentityServerBuilder AddInMemoryCaching(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddTransient(typeof(ICache<>), typeof(DefaultCache<>));

            return builder;
        }

        static void AddTransientDecorator<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            services.AddDecorator<TService>();
            services.AddTransient<TService, TImplementation>();
        }

        static void AddDecorator<TService>(this IServiceCollection services)
        {
            var registration = services.FirstOrDefault(x => x.ServiceType == typeof(TService));
            if (registration == null)
            {
                throw new InvalidOperationException("Service type: " + typeof(TService).Name + " not registered.");
            }
            if (services.Any(x => x.ServiceType == typeof(Decorator<TService>)))
            {
                throw new InvalidOperationException("Decorator already registered for type: " + typeof(TService).Name + ".");
            }

            services.Remove(registration);

            if (registration.ImplementationInstance != null)
            {
                var type = registration.ImplementationInstance.GetType();
                var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), type);
                services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
                services.Add(new ServiceDescriptor(type, registration.ImplementationInstance));
            }
            else if (registration.ImplementationFactory != null)
            {
                services.Add(new ServiceDescriptor(typeof(Decorator<TService>), provider =>
                {
                    return new DisposableDecorator<TService>((TService)registration.ImplementationFactory(provider));
                }, registration.Lifetime));
            }
            else
            {
                var type = registration.ImplementationType;
                var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), registration.ImplementationType);
                services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
                services.Add(new ServiceDescriptor(type, type, registration.Lifetime));
            }
        }

        public static IIdentityServerBuilder AddInMemoryScopes(this IIdentityServerBuilder builder, IEnumerable<Scope> scopes)
        {
            builder.Services.AddSingleton(scopes);
            builder.Services.AddTransient<IScopeStore, InMemoryScopeStore>();

            return builder;
        }

        public static IIdentityServerBuilder AddInMemoryClients(this IIdentityServerBuilder builder, IEnumerable<Client> clients)
        {
            builder.Services.AddSingleton(clients);

            builder.Services.AddTransient<IClientStore, InMemoryClientStore>();
            builder.Services.AddTransient<ICorsPolicyService, InMemoryCorsPolicyService>();

            return builder;
        }

        public static IIdentityServerBuilder AddInMemoryUsers(this IIdentityServerBuilder builder, List<InMemoryUser> users)
        {
            builder.Services.AddSingleton(users);

            builder.Services.AddTransient<IProfileService, InMemoryUserProfileService>();
            builder.Services.AddTransient<IResourceOwnerPasswordValidator, InMemoryUserResourceOwnerPasswordValidator>();
            builder.Services.AddTransient<InMemoryUserLoginService>();

            return builder;
        }

        public static IIdentityServerBuilder AddInMemoryStores(this IIdentityServerBuilder builder)
        {
            builder.Services.TryAddSingleton<IPersistedGrantStore, InMemoryPersistedGrantStore>();

            return builder;
        }

        public static IIdentityServerBuilder AddExtensionGrantValidator<T>(this IIdentityServerBuilder builder)
            where T : class, IExtensionGrantValidator
        {
            builder.Services.AddTransient<IExtensionGrantValidator, T>();

            return builder;
        }

        public static IIdentityServerBuilder AddResourceOwnerValidator<T>(this IIdentityServerBuilder builder)
           where T : class, IResourceOwnerPasswordValidator
        {
            builder.Services.AddTransient<IResourceOwnerPasswordValidator, T>();

            return builder;
        }

        public static IIdentityServerBuilder AddProfileService<T>(this IIdentityServerBuilder builder)
           where T : class, IProfileService
        {
            builder.Services.AddTransient<IProfileService, T>();

            return builder;
        }

        public static IIdentityServerBuilder AddSecretParser<T>(this IIdentityServerBuilder builder)
            where T : class, ISecretParser
        {
            builder.Services.AddTransient<ISecretParser, T>();

            return builder;
        }

        public static IIdentityServerBuilder AddSecretValidator<T>(this IIdentityServerBuilder builder)
            where T : class, ISecretValidator
        {
            builder.Services.AddTransient<ISecretValidator, T>();

            return builder;
        }

        /// <summary>
        /// Adds the client store cache.
        /// </summary>
        /// <typeparam name="T">The type of the concrete client store class that is registered in DI.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <returns></returns>
        public static IIdentityServerBuilder AddClientStoreCache<T>(this IIdentityServerBuilder builder)
            where T : IClientStore
        {
            builder.Services.AddTransient<IClientStore, CachingClientStore<T>>();
            return builder;
        }
        
        /// <summary>
        /// Adds the client store cache.
        /// </summary>
        /// <typeparam name="T">The type of the concrete scope store class that is registered in DI.</typeparam>
        /// <param name="builder">The builder.</param>
        /// <returns></returns>
        public static IIdentityServerBuilder AddScopeStoreCache<T>(this IIdentityServerBuilder builder)
            where T : IScopeStore
        {
            builder.Services.AddTransient<IScopeStore, CachingScopeStore<T>>();
            return builder;
        }

        public static IIdentityServerBuilder SetSigningCredential(this IIdentityServerBuilder builder, SigningCredentials credential)
        {
            // todo
            //if (!(credential.Key is AsymmetricSecurityKey) &&
            //    !credential.Key.IsSupportedAlgorithm(SecurityAlgorithms.RsaSha256Signature))
            //{
            //    throw new InvalidOperationException("Signing key is not asymmetric and does not support RS256");
            //}

            builder.Services.AddSingleton<ISigningCredentialStore>(new DefaultSigningCredentialsStore(credential));
            builder.Services.AddSingleton<IValidationKeysStore>(new DefaultValidationKeysStore(new[] { credential.Key }));

            return builder;
        }

        public static IIdentityServerBuilder SetSigningCredential(this IIdentityServerBuilder builder, X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("X509 certificate does not have a private key.");
            }

            var credential = new SigningCredentials(new X509SecurityKey(certificate), "RS256");
            return builder.SetSigningCredential(credential);
        }

        public static IIdentityServerBuilder SetSigningCredential(this IIdentityServerBuilder builder, string name, StoreLocation location = StoreLocation.LocalMachine)
        {
            X509Certificate2 certificate;

            if (location == StoreLocation.LocalMachine)
            {
                certificate = X509.LocalMachine.My.SubjectDistinguishedName.Find(name, validOnly: false).FirstOrDefault();
            }
            else
            {
                certificate = X509.CurrentUser.My.SubjectDistinguishedName.Find(name, validOnly: false).FirstOrDefault();
            }

            if (certificate == null) throw new InvalidOperationException($"certificate: '{name}' not found in certificate store");

            return builder.SetSigningCredential(certificate);
        }

        public static IIdentityServerBuilder SetSigningCredential(this IIdentityServerBuilder builder, RsaSecurityKey rsaKey)
        {
            if (!rsaKey.HasPrivateKey)
            {
                throw new InvalidOperationException("RSA key does not have a private key.");
            }

            var credential = new SigningCredentials(rsaKey, "RS256");
            return builder.SetSigningCredential(credential);
        }

        public static IIdentityServerBuilder SetTemporarySigningCredential(this IIdentityServerBuilder builder)
        {
            var rsa = RSA.Create();

#if NET452
            if (rsa.KeySize < 2048)
            {
                rsa.Dispose();
                rsa = new RSACryptoServiceProvider(2048);
            }
#endif
            RsaSecurityKey key = null;
#if NET452
            if (rsa is RSACryptoServiceProvider) 
            {
                var parameters = rsa.ExportParameters(includePrivateParameters: true);
                key = new RsaSecurityKey(parameters);
                        
                rsa.Dispose();
            }   
#endif
            if (key == null)
            {
                key = new RsaSecurityKey(rsa);
            }

            key.KeyId = CryptoRandom.CreateUniqueId();
            
            var credential = new SigningCredentials(key, "RS256");
            return builder.SetSigningCredential(credential);
        }

        public static IIdentityServerBuilder AddValidationKeys(this IIdentityServerBuilder builder, params AsymmetricSecurityKey[] keys)
        {
            builder.Services.AddSingleton<IValidationKeysStore>(new DefaultValidationKeysStore(keys));

            return builder;
        }
    }
}