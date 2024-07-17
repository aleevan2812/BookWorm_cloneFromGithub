﻿using System.Text;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BookWorm.ServiceDefaults;

public sealed class ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider provider, IConfiguration config)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }

        ConfigureAuthorization(options);
    }

    private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var openApi = config.GetSection("OpenApi");
        var document = openApi.GetRequiredSection("Document");

        var info = new OpenApiInfo
        {
            Title = document.GetValue<string>("Title"),
            Version = description.ApiVersion.ToString(),
            Description = BuildDescription(description, document.GetValue<string>("Description")),
            Contact = new()
            {
                Name = "Nhan Nguyen",
                Email = "nguyenxuannhan407@gmail.com",
                Url = new("https://github.com/foxminchan")
            },
            License = new() { Name = "MIT", Url = new("https://opensource.org/licenses/MIT") }
        };

        return info;
    }

    private static string BuildDescription(ApiVersionDescription api, string? description)
    {
        if (description is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder(description);

        if (api.IsDeprecated)
        {
            if (text.Length > 0)
            {
                if (text[^1] != '.')
                {
                    text.Append('.');
                }

                text.Append(' ');
            }

            text.Append("This API version has been deprecated.");
        }

        if (api.SunsetPolicy is not { } policy)
        {
            return text.ToString();
        }

        if (policy.Date is { } when)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append("The API will be sunset on ")
                .Append(when.Date.ToShortDateString())
                .Append('.');
        }

        if (!policy.HasLinks)
        {
            return text.ToString();
        }

        text.AppendLine();

        var rendered = false;

        foreach (var link in policy.Links.Where(l => l.Type == "text/html"))
        {
            if (!rendered)
            {
                text.Append("<h4>Links</h4><ul>");
                rendered = true;
            }

            text.Append("<li><a href=\"");
            text.Append(link.LinkTarget.OriginalString);
            text.Append("\">");
            text.Append(
                StringSegment.IsNullOrEmpty(link.Title)
                    ? link.LinkTarget.OriginalString
                    : link.Title.ToString());
            text.Append("</a></li>");
        }

        if (rendered)
        {
            text.Append("</ul>");
        }

        return text.ToString();
    }

    private void ConfigureAuthorization(SwaggerGenOptions options)
    {
        var identitySection = config.GetSection("Identity");

        var identityUrlExternal = identitySection.GetValue<string>("Url");
        var scopes = identitySection.GetRequiredSection("Scopes").GetChildren().ToDictionary(p => p.Key, p => p.Value);

        options.AddSecurityDefinition("oauth2",
            new()
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new()
                {
                    Implicit = new()
                    {
                        AuthorizationUrl = new($"{identityUrlExternal}/connect/authorize"),
                        TokenUrl = new($"{identityUrlExternal}/connect/token"),
                        Scopes = scopes,
                    }
                }
            });

        options.OperationFilter<AuthorizeCheckOperationFilter>([scopes.Keys.ToArray()]);
    }
}
