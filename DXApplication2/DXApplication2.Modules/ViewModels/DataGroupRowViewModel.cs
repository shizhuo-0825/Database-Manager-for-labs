using DXApplication2.Common.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DXApplication2.Modules.ViewModels
{
    public class DataGroupRowViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public DateTime ExperimentDate { get; set; }
        public string Material { get; set; } = string.Empty;

        private string _sourceTypes = string.Empty;
        public string SourceTypes
        {
            get => _sourceTypes;
            set { _sourceTypes = value; OnPropertyChanged(); IsDirty = true; }
        }

        private string _methodTypes = string.Empty;
        public string MethodTypes
        {
            get => _methodTypes;
            set { _methodTypes = value; OnPropertyChanged(); IsDirty = true; }
        }

        private string _pumpTypes = string.Empty;
        public string PumpTypes
        {
            get => _pumpTypes;
            set { _pumpTypes = value; OnPropertyChanged(); IsDirty = true; }
        }

        // 内部标记:该行是否被修改过,决定保存时是否需要更新
        public bool IsDirty { get; set; }
        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); IsDirty = true; }
        }
        // 引用原始 DataGroup 对象,方便保存时定位数据库记录
        public DataGroup? OriginalGroup { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}