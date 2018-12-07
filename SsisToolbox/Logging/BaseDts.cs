using SsisToolbox.Interface;

namespace SsisToolbox.Logging
{
    public abstract class BaseDts: IDts
    {
        public abstract void FireInformation(int informationCode, string subComponent, string description, string helpFile, int helpContext, ref bool fireAgain);
        public void FireInformation(string subComponent, string description)
        {
            bool fireAgain = true;
            FireInformation(0, subComponent, description, "", 0, ref fireAgain);
        }

        public abstract void FireWarning(int warningCode, string subComponent, string description, string helpFile, int helpContext);
        public void FireWarning(string subComponent, string description)
        {
            FireWarning(0, subComponent, description, "", 0);
        }

        public abstract void FireError(int warningCode, string subComponent, string description, string helpFile, int helpContext);
        public void FireError(string subComponent, string description)
        {
            FireError(0, subComponent, description, "", 0);
        }

        public abstract int SuccessCode { get; }
        public abstract int FailureCode { get; }
    }
}
