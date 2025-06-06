global using System;
global using System.Collections.Generic;
global using System.Text;
global using System.Threading.Tasks;
global using System.Linq;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.Json.Nodes;

global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;

global using BotSharp.Abstraction.Conversations.Models;
global using BotSharp.Abstraction.Agents.Constants;
global using BotSharp.Abstraction.Agents.Models;
global using BotSharp.Abstraction.MLTasks;
global using BotSharp.Abstraction.Utilities;
global using BotSharp.Abstraction.Realtime;
global using BotSharp.Abstraction.Realtime.Models;
global using BotSharp.Core.Infrastructures;
global using BotSharp.Abstraction.Agents;
global using BotSharp.Abstraction.Agents.Enums;
global using BotSharp.Abstraction.Conversations;
global using BotSharp.Abstraction.Conversations.Enums;
global using BotSharp.Abstraction.Functions.Models;
global using BotSharp.Abstraction.Loggers;

global using BotSharp.Plugin.GoogleAi.Settings;
global using BotSharp.Plugin.GoogleAi.Providers.Chat;