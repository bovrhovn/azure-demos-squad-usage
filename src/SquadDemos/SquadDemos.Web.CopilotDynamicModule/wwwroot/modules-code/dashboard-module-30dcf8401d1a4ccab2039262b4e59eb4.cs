using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SquadDemos.Web.CopilotDynamicModule.Generated
{
    public sealed class AboutCopilotModule : SquadDemos.Web.CopilotDynamicModule.Features.Dashboard.ICopilotModule
    {
        private string _configKey = string.Empty;
        private string _configValue = string.Empty;

        public int Order => 100;

        public Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var html =
                "<section class=\"card shadow-sm mb-3\">" +
                "<div class=\"card-body\">" +
                "<h3 class=\"card-title\">Who am I?</h3>" +
                "<p class=\"card-text\">I am <strong>GitHub Copilot</strong>, an AI coding assistant built by GitHub, " +
                "powered by Claude Sonnet 5.5.</p>" +
                "<h4>What do I do?</h4>" +
                "<ul>" +
                "<li>Write, explain and refactor code in many languages</li>" +
                "<li>Generate dashboard modules like this one</li>" +
                "<li>Debug issues, run builds and tests, and review changes</li>" +
                "<li>Answer technical questions and help automate developer workflows</li>" +
                "</ul>" +
                "<p class=\"text-muted small mb-0\">Generated at " + DateTime.UtcNow.ToString("u") + "</p>" +
                "</div>" +
                "</section>";

            return Task.FromResult(html);
        }

        public void SetConfiguration(KeyValuePair<string, string> configuration)
        {
            _configKey = configuration.Key ?? string.Empty;
            _configValue = configuration.Value ?? string.Empty;
        }
    }
}