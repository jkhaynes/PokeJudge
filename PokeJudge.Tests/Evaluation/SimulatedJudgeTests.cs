namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;
using PokeJudge.Tests.TestDoubles;

public class SimulatedJudgeTests
{
    [Fact]
    public async Task AnswerAsync_SendsFactSheetAndQuestionToTheModel()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("It was noticed on turn 3.", true));
        var judge = new SimulatedJudge(llm);

        await judge.AnswerAsync("The error was noticed on turn 3.", "When was it noticed?");

        Assert.Contains("The error was noticed on turn 3.", llm.UserContents[0]);
        Assert.Contains("When was it noticed?", llm.UserContents[0]);
    }

    [Fact]
    public async Task AnswerAsync_Known_ReturnsTheModelsAnswer()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("It was noticed on turn 3.", true));

        var answer = await new SimulatedJudge(llm).AnswerAsync("facts", "question?");

        Assert.True(answer.Known);
        Assert.Equal("It was noticed on turn 3.", answer.Answer);
    }

    [Fact]
    public async Task AnswerAsync_NotKnown_ReturnsTheStandardNotKnownAnswer()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("I'm not sure, maybe turn 2?", false));

        var answer = await new SimulatedJudge(llm).AnswerAsync("facts", "question?");

        Assert.False(answer.Known);
        Assert.Equal(SimulatedJudge.NotKnown, answer.Answer);
    }
}
