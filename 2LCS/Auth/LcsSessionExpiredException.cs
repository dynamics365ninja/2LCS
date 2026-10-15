using System;

namespace LCS.Auth
{
    public sealed class LcsSessionExpiredException : Exception
    {
        public LcsSessionExpiredException()
            : base("The LCS session has expired and could not be renewed automatically.")
        {
        }

        public static bool IsCauseOf(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is LcsSessionExpiredException) return true;
                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        if (IsCauseOf(inner)) return true;
                    }
                }
            }
            return false;
        }
    }
}
