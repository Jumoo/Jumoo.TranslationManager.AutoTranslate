using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Jumoo.TranslationManager.AutoTranslate;
internal class AutomaticApproverNotificationHandler :
    INotificationAsyncHandler<ContentSavedNotification>,
    INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly AutomaticTranslationService _automaticTranslationService;
    private readonly ILogger<AutomaticApproverNotificationHandler> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;


    public AutomaticApproverNotificationHandler(
        IConfiguration configuration,
        AutomaticTranslationService automaticTranslationService,
        ILogger<AutomaticApproverNotificationHandler> logger,
        IBackgroundTaskQueue taskQueue)
    {
        _configuration = configuration;
        _automaticTranslationService = automaticTranslationService;
        _logger = logger;
        _taskQueue = taskQueue;
    }

    public async Task HandleAsync(ContentSavedNotification notification, CancellationToken cancellationToken)
    {
        if (_configuration.GetValue("Translation:Auto:OnSave", false) is false) return;
        await TranslateContentNodes(notification.SavedEntities);
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        if (_configuration.GetValue("Translation:Auto:OnPublish", false) is false) return;
        await TranslateContentNodes(notification.PublishedEntities);
    }

    private Task TranslateContentNodes(IEnumerable<IContent> items)
    {
        try
        {
            // work out which cultures have just been published. 
            // then when we are using variants we can work out if 
            // this is the "master" culture and if we need to create
            // a job. 
            var cultures = items.FirstOrDefault()?
                .PublishCultureInfos?.Values
                .Where(x => x.WasDirty())
                .Select(x => x.Culture) ?? Enumerable.Empty<string>();

            // send it off to the background (won't block the publish).
            _taskQueue.QueueBackgroundWorkItem(async token =>
            {
                await _automaticTranslationService.TranslateAsync(items, cultures);
            });
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error translating content");
        }

        return Task.CompletedTask;
    }
}
