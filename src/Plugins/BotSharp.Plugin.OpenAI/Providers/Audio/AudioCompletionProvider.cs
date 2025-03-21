namespace BotSharp.Plugin.OpenAI.Providers.Audio;

public partial class AudioCompletionProvider : IAudioCompletion
{
    private readonly IServiceProvider _services;

    public string Provider => "openai";
    public string Model => _model;

    private string _model;

    public AudioCompletionProvider(IServiceProvider service)
    {
        _services = service;
    }

    public void SetModelName(string model)
    {
        _model = model;
    }
}
