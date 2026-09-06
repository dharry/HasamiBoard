namespace HasamiBoard.ViewModels;

/// <summary>Cron式ジェネレーターで選択可能な頻度テンプレート。Customは生成を行わず、Cron式を直接編集する。</summary>
public enum CronFrequency
{
    EverySecond,
    EveryMinute,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Custom,
}
