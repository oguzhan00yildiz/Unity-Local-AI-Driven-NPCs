namespace AISystem
{
    /// <summary>
    /// A component on an NPC that adds text to the NPC's system prompt (actions, story knowledge, world state).
    /// NPCAgent.RefreshSystemPrompt joins the personality prompt and the text of every source.
    /// </summary>
    public interface INPCPromptSource
    {
        /// <summary>Text appended to the system prompt. Return an empty string to add nothing.</summary>
        string GetPromptText();
    }
}
