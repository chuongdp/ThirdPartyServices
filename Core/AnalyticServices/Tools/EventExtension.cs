namespace Core.AnalyticServices.Tools
{
    using Core.AnalyticServices.Data;
    using Utilities.Extension;

    /// <summary>
    /// 
    /// </summary>
    public static class EventExtension
    {
        // todo - this should be able to be handled at compile time
        public static string ToSnakeCase(this IEvent trackedEvent)
        {
            return trackedEvent.GetType().Name.ToSnakeCase();
        }
    }
}