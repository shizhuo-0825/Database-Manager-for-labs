using DevExpress.Mvvm;

namespace DXApplication2.Modules.ViewModels
{
    public class ProgressSplashViewModel : ViewModelBase
    {
        double progress;
        double maximum = 100;
        string status = "";

        public double Progress
        {
            get => progress;
            set => SetProperty(ref progress, value, nameof(Progress));
        }

        public double Maximum
        {
            get => maximum;
            set => SetProperty(ref maximum, value, nameof(Maximum));
        }

        public string Status
        {
            get => status;
            set => SetProperty(ref status, value, nameof(Status));
        }

        public string PercentText
            => $"{Progress / Maximum:P1}";
    }
}
