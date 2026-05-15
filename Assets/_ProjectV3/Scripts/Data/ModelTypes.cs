namespace ChemLabSimV3.Data.Models
{
    /// <summary>Minimal quiz question type.</summary>
    public class QuizQuestion
    {
        public string Question { get; set; }
        public string[] AnswerOptions { get; set; }
    }

    /// <summary>Minimal challenge definition.</summary>
    public class ChallengeDef
    {
        public string Title { get; set; }
        public int Level { get; set; }
    }

    /// <summary>Minimal objective condition type.</summary>
    public class ObjectiveCondition
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }

    /// <summary>Minimal achievement definition.</summary>
    public class AchievementDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
