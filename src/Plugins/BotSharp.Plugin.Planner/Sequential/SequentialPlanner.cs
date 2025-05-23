/*****************************************************************************
  Copyright 2024 Written by Haiping Chen. All Rights Reserved.

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
******************************************************************************/

namespace BotSharp.Plugin.Planner.Sequential;

/// <summary>
/// A Sequential Planner for a large language model (LLM) is a framework or methodology to execute some list tasks in a predefined order by the user, 
/// LLM can follow to produce an organized and coherent output. 
/// Sequential Planners are useful for tasks that involve multiple stages of reasoning, information retrieval, or interdependent subtasks.
/// </summary>
public class SequentialPlanner : ITaskPlanner
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    public string Name => "Sequential-Planner";
    public bool HideDialogContext => true;
    public int MaxLoopCount => 100;
    private FunctionCallFromLlm _lastInst;

    public SequentialPlanner(IServiceProvider services, ILogger<SequentialPlanner> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<FunctionCallFromLlm> GetNextInstruction(Agent router, string messageId, List<RoleDialogModel> dialogs)
    {
        var decomposation = await GetDecomposedStepAsync(router, messageId, dialogs);
        if (decomposation.TotalRemainingSteps > 0 && _lastInst != null)
        {
            // _lastInst.Response = decomposation.Description;
            _lastInst.NextActionReason = $"Having {decomposation.TotalRemainingSteps} steps left.";
            return _lastInst;
        }
        else if (decomposation.TotalRemainingSteps == 0 || decomposation.ShouldStop)
        {
            if (!string.IsNullOrEmpty(decomposation.StopReason))
            {
                // Tell router all steps are done
                dialogs.Add(new RoleDialogModel(AgentRole.Assistant, decomposation.StopReason)
                {
                    CurrentAgentId = router.Id,
                    MessageId = messageId
                });
                router.TemplateDict["conversation"] = router.TemplateDict["conversation"].ToString().TrimEnd() +
                    $"\r\n{router.Name}: {decomposation.StopReason}";
            }
        }

        var next = GetNextStepPrompt(router);

        // text completion
        /*var agentService = _services.GetRequiredService<IAgentService>();
        var instruction = agentService.RenderedInstruction(router);
        var content = $"{instruction}\r\n###\r\n{next}";
        content =  content + "\r\nResponse: ";
        var completion = CompletionProvider.GetTextCompletion(_services);*/

        // chat completion
        var completion = CompletionProvider.GetChatCompletion(_services,
            provider: router?.LlmConfig?.Provider,
            model: router?.LlmConfig?.Model);


        string text = string.Empty;

        // text completion
        // text = await completion.GetCompletion(content, router.Id, messageId);
        dialogs = new List<RoleDialogModel>
        {
            new RoleDialogModel(AgentRole.User, next)
            {
                FunctionName = nameof(SequentialPlanner),
                MessageId = messageId
            }
        };
        var response = await completion.GetChatCompletions(router, dialogs);

        var inst = response.Content.JsonContent<FunctionCallFromLlm>();

        if (decomposation.TotalRemainingSteps > 0)
        {
            // inst.Response = decomposation.Description;
            inst.NextActionReason = $"{decomposation.TotalRemainingSteps} steps left.";
            inst.HandleDialogsByPlanner = true;
        }

        _lastInst = inst;
        return inst;
    }

    public List<RoleDialogModel> BeforeHandleContext(FunctionCallFromLlm inst, RoleDialogModel message, List<RoleDialogModel> dialogs)
    {
        var taskAgentDialogs = new List<RoleDialogModel>
        {
            /*new RoleDialogModel(AgentRole.User, inst.Response)
            {
                MessageId = message.MessageId,
            }*/
        };

        return taskAgentDialogs;
    }

    public bool AfterHandleContext(List<RoleDialogModel> dialogs, List<RoleDialogModel> taskAgentDialogs)
    {
        dialogs.AddRange(taskAgentDialogs.Skip(1));
        return true;
    }

    public async Task<bool> AgentExecuting(Agent router, FunctionCallFromLlm inst, RoleDialogModel message, List<RoleDialogModel> dialogs)
    {
        // Set user content as Planner's question
        message.FunctionName = inst.Function;
        message.FunctionArgs = inst.Arguments == null ? "{}" : JsonSerializer.Serialize(inst.Arguments);

        return true;
    }

    public async Task<bool> AgentExecuted(Agent router, FunctionCallFromLlm inst, RoleDialogModel message, List<RoleDialogModel> dialogs)
    {
        var context = _services.GetRequiredService<IRoutingContext>();

        if (message.StopCompletion)
        {
            context.Empty(reason: $"Agent queue is cleared by {nameof(SequentialPlanner)}");
            return false;
        }

        // Handover to Router;
        context.Pop();

        var routing = _services.GetRequiredService<IRoutingService>();
        routing.Context.ResetRecursiveCounter();

        return true;
    }

    private string GetNextStepPrompt(Agent router)
    {
        var template = router.Templates.First(x => x.Name == "reasoner.sequential").Content;

        var render = _services.GetRequiredService<ITemplateRender>();
        return render.Render(template, new Dictionary<string, object>
        {
        });
    }

    public async Task<DecomposedStep> GetDecomposedStepAsync(Agent router, string messageId, List<RoleDialogModel> dialogs)
    {
        var systemPrompt = GetDecomposeTaskPrompt(router);

        var inst = new DecomposedStep();

        var llmProviderService = _services.GetRequiredService<ILlmProviderService>();
        var model = llmProviderService.GetProviderModel("openai", "gpt-4o");

        // chat completion
        var completion = CompletionProvider.GetChatCompletion(_services,
            provider: "openai",
            model: model.Name);

        int retryCount = 0;
        while (retryCount < 2)
        {
            string text = string.Empty;
            try
            {
                var response = await completion.GetChatCompletions(new Agent
                {
                    Id = router.Id,
                    Name = nameof(SequentialPlanner),
                    Instruction = systemPrompt
                }, dialogs);

                text = response.Content;
                inst = response.Content.JsonContent<DecomposedStep>();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message}: {text}");
            }
            finally
            {
                retryCount++;
            }
        }

        return inst;
    }

    private string GetDecomposeTaskPrompt(Agent router)
    {
        var template = router.Templates.First(x => x.Name == "reasoner.sequential.get_remaining_task").Content;

        var render = _services.GetRequiredService<ITemplateRender>();
        return render.Render(template, new Dictionary<string, object>
        {
        });
    }
}
