using Jumoo.TranslationManager.Core.Boot;

using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Jumoo.TranslationManager.AutoTranslate;

[ComposeAfter(typeof(TranslationComposer))]
public class AutomaticApprovalComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<AutomaticTranslationService>();

        builder.AddNotificationAsyncHandler<ContentSavedNotification, AutomaticApproverNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, AutomaticApproverNotificationHandler>();
    }
}
