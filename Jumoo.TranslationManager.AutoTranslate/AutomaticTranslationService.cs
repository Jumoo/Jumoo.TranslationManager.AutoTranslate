using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;
using Jumoo.TranslationManager.Core.Services;

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop.Implementation;

using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Jumoo.TranslationManager.AutoTranslate;

internal class AutomaticTranslationService
{
    private readonly ILogger<AutomaticTranslationService> _logger;

    private readonly TranslationSetService _setService;
    private readonly IContentService _contentService;
    private readonly TranslationProviderCollection _providers;
    private readonly TranslationJobService _jobService;
    private readonly TranslationNodeService _nodeService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly ITranslationAprovalService _aprovalService;

    private readonly Guid _providerKey;
    private static Guid microsoftKey = Guid.Parse("F9CD9683-5F0A-4407-9E5D-BD7295FEFEB1");

    private readonly List<int> _excludedSets = new List<int>();
    private readonly List<string> _excludedCultures = new List<string>();

    public AutomaticTranslationService(
        TranslationSetService setService,
        IContentService contentService,
        TranslationProviderCollection providers,
        TranslationJobService jobService,
        TranslationNodeService nodeService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ITranslationAprovalService aprovalService,
        IConfiguration configuration,
        ILogger<AutomaticTranslationService> logger)
    {
        _logger = logger;

        _setService = setService;
        _contentService = contentService;
        _providers = providers;
        _jobService = jobService;
        _nodeService = nodeService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _aprovalService = aprovalService;

        _providerKey = configuration.GetValue("Translation:Auto:Provider", microsoftKey);
        _excludedSets = configuration.GetSection("Translation:Auto:ExcludeSets").Get<List<int>>() ?? new List<int>();
        _excludedCultures = configuration.GetSection("Translation:Auto:ExcludeCultures").Get<List<string>>() ?? new List<string>();
    }

    public async Task TranslateAsync(IEnumerable<int> ids, IEnumerable<string> cultures)
        => await TranslateAsync(_contentService.GetByIds(ids), cultures);

    public async Task TranslateAsync(IEnumerable<IContent> items, IEnumerable<string> cultures)
    {
        var sets = await GetSetsAsync(items, cultures);
        // this isn't actually bad, if the content doesn't belong to a set, it might be 
        // from a target site ?
        if (sets.Count == 0) return; 

        // get the microsoft provider
        var provider = GetProvider(_providerKey);
        if (provider is null)
            throw new NotImplementedException("Provider not found");

        // translations can't be mixed per set, so this translates for a give set. 
        foreach(var set in sets)
        {
            // get the nodes
            var nodes = await CreateTranslationNodesAsync(set, items);
            if (nodes.Count == 0) continue;

            // create the jobs
            var jobs = await CreateJobsAsync(nodes, provider);
            if (jobs.Count == 0) continue;
            _logger.LogInformation("Auto created {count} jobs.", jobs.Count);

            // submit the jobs. (do the work).
            var submittedJobs = await SubmitJobsAsync(jobs);
            if (submittedJobs.Count == 0) continue;
            _logger.LogInformation("Auto submitted {count} jobs.", submittedJobs.Count);

            //var approvedJobs = await ApproveJobsAsync(submittedJobs);
            //_logger.LogInformation("Auto translated {count} jobs.", approvedJobs.Count);    
        }
    }

    /// <summary>
    ///  get the correct sets for the content items, 
    /// </summary>
    /// <remarks>
    ///  gets the sets but only returns the ones that are valid 
    ///  for the culture or not excluded by settings. 
    /// </remarks>
    private async Task<List<TranslationSet>> GetSetsAsync(IEnumerable<IContent> items, IEnumerable<string> cultures)
    {
        var hasCultures = cultures.Any();

        var translationSets = new List<TranslationSet>();

        foreach (var item in items)
        {
            translationSets.AddRange(_setService.GetSetsByPath(item.Path));
        }

        return translationSets
            .DistinctBy(x => x.Id)
            .Where(x => hasCultures is false || cultures.Contains(x.Culture.Name, StringComparer.OrdinalIgnoreCase))
            .Where(x => _excludedSets.Contains(x.Id) is false)
            .ToList();
    }

    private async Task<List<TranslationNode>> CreateTranslationNodesAsync(TranslationSet set, IEnumerable<IContent> items)
    {
        var nodes = new List<TranslationNode>();

        // create the nodes. 
        var nodeOptions = new NodeCreationOptions
        {
            ChangeType = TranslationChangeType.Force,
            DefaultNodeStatus = NodeStatus.Open,
            IncludeNameChange = true,
        };

        var sites = set.Sites.Where(x => _excludedCultures.Contains(x.Culture.DisplayName, StringComparer.OrdinalIgnoreCase) is false);

        foreach (var item in items)
        {
            nodes.AddRange(_nodeService.CreateNodes(set, item, nodeOptions, sites));
        }

        return nodes;
    }


    private async Task<List<TranslationJob>> CreateJobsAsync(List<TranslationNode> nodes, ITranslationProvider provider)
    {
        var jobs = new List<TranslationJob>();

        var jobOptions = new JobOptions
        {
            AutoApprove = true,
        };

        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        var groupId = Guid.NewGuid().ToString();

        // group the nodes by culture, so we create one job per language 
        foreach (var groupedNodes in nodes.GroupBy(x => x.Culture))
        {
            var firstNode = groupedNodes.FirstOrDefault();
            if (firstNode is null) continue;

            var name = $"{firstNode.MasterNodeName} ({groupedNodes.Count()}) automatic translation to {firstNode.Culture.DisplayName}";

            var job = _jobService.CreateJob(name, groupedNodes, provider, new object(), user?.Id ?? -1, jobOptions, groupId);
            if (job is null) continue;

            jobs.Add(job);
        }

        return jobs;
    }

    private async Task<List<TranslationJob>> SubmitJobsAsync(List<TranslationJob> jobs)
    {
        var submittedJobs = new List<TranslationJob>();
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

        foreach (var j in jobs) {
            // make sure we've loaded the nodes before we submit the job
            var job = await _jobService.LoadJobNodesAsync(j);
            if (job is null) continue;

            var result = await _jobService.SubmitJob(job);
            if (result.Success is false)
                throw result.Exception ?? new Exception("Failed to submit job");

            submittedJobs.Add(job);
        }

        return submittedJobs;
    }

    /// <summary>
    ///  approve
    /// </summary>
    /// <remarks>
    ///  you could approve like this, but if you fire the job in with 'autoapprove' set to true, 
    ///  then when the job comes back the auto-approver in translation manager will approve it 
    ///  in the background for you. 
    /// </remarks>
    private async Task<List<TranslationJob>> ApproveJobsAsync(List<TranslationJob> jobs)
    { 
        var approvedJobs = new List<TranslationJob>();
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

        foreach (var job in jobs) { 

            var approvalResult = await _aprovalService.ApproveAsync(job.Id, new TranslationJobApprovalOptions
            {
                Approve = true,
                // if you set approveAllNodes = true, you don't need to pass nodes,
                // it will just load them all for the job and approve it. 
                ApproveAllNodes = true,
                Check = true,
                Publish = true,
                UserId = user?.Id ?? -1,
            });

            if (approvalResult is false)
                throw new Exception("Failed to approve job");

            approvedJobs.Add(job);
        }

        return approvedJobs;
    }

    private ITranslationProvider? GetProvider(Guid providerKey)
        => _providers.GetProvider(providerKey);
}
