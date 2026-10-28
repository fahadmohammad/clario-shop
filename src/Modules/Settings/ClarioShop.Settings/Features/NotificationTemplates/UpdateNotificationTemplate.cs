using ClarioShop.Notifications.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;
using Npgsql;

namespace ClarioShop.Settings.Features.NotificationTemplates;

/// <summary>
/// Saves the store's own text for one message. Emails need a subject, SMS have none. Placeholders must be ones the
/// message has, so a typo never reaches a shopper.
/// </summary>
public static class UpdateNotificationTemplate
{
    /// <param name="Subject">Required for email; leave it out for SMS.</param>
    public sealed record Input(string? Subject, string Body);

    public sealed record Command(string Event, string Channel, string Language, Input Template);

    internal sealed class Validator : AbstractValidator<Input>
    {
        public Validator()
        {
            RuleFor(input => input.Body).NotEmpty().MaximumLength(NotificationTemplate.MaxEmailBodyLength);
            RuleFor(input => input.Subject).MaximumLength(NotificationTemplate.MaxSubjectLength);
        }
    }

    internal sealed class Handler(SettingsDbContext db, INotificationTemplateDefaults defaults, SettingsCache cache, TimeProvider clock)
        : ICommandHandler<Command, NotificationTemplateResponse>
    {
        public async Task<Result<NotificationTemplateResponse>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (defaults.Find(command.Event, command.Channel, command.Language) is not { } definition)
            {
                return StoreSettingsErrors.NotificationTemplateNotFound;
            }

            var subject = string.IsNullOrWhiteSpace(command.Template.Subject) ? null : command.Template.Subject.Trim();
            var body = command.Template.Body.Trim();
            if (Check(definition, subject, body) is { } problem)
            {
                return problem;
            }

            var template = await db.NotificationTemplates.FirstOrDefaultAsync(
                t => t.Event == definition.Event && t.Channel == definition.Channel && t.Language == definition.Language,
                cancellationToken);
            var now = clock.GetUtcNow();
            if (template is null)
            {
                template = NotificationTemplate.Create(definition.Event, definition.Channel, definition.Language, subject, body, now);
                db.NotificationTemplates.Add(template);
            }
            else
            {
                template.Update(subject, body, now);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
                || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return StoreSettingsErrors.Concurrency;
            }

            await cache.InvalidateAsync(cancellationToken);
            return NotificationTemplateResponse.From(definition, template);
        }

        private static Error? Check(NotificationTemplateDefinition definition, string? subject, string body)
        {
            if (definition.IsEmail() && subject is null)
            {
                return Error.Validation("subject", "Emails need a subject.");
            }

            if (!definition.IsEmail() && subject is not null)
            {
                return Error.Validation("subject", "SMS have no subject; leave it out.");
            }

            if (!definition.IsEmail() && body.Length > NotificationTemplate.MaxSmsBodyLength)
            {
                return Error.Validation("body", $"An SMS can be at most {NotificationTemplate.MaxSmsBodyLength} characters.");
            }

            if (subject is not null && TemplateText.FindProblem(subject, definition.Placeholders) is { } subjectProblem)
            {
                return Error.Validation("subject", subjectProblem);
            }

            return TemplateText.FindProblem(body, definition.Placeholders) is { } bodyProblem
                ? Error.Validation("body", bodyProblem)
                : null;
        }
    }
}
