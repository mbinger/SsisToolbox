using System;

namespace SsisToolbox.Logging
{
    /// <summary>
    /// Console adapter for events object wrapper
    /// </summary>
    public class ConsoleDtsAdapter : BaseDts
    {
        public override void FireInformation(int informationCode, string subComponent, string description, string helpFile, int helpContext, ref bool fireAgain)
        {
            Console.WriteLine($"[INFO] {subComponent} {description}");
        }

        public override void FireWarning(int warningCode, string subComponent, string description, string helpFile, int helpContext)
        {
            Console.WriteLine($"[WARN] {subComponent} {description}");
        }

        public override void FireError(int warningCode, string subComponent, string description, string helpFile, int helpContext)
        {
            Console.WriteLine($"[ERR] {subComponent} {description}");
        }

        public override int SuccessCode
        {
            get
            {
                return 1;
            }
        }
        public override int FailureCode
        {
            get
            {
                return 0;
            }
        }
    }
}
