using System.Windows;
using HandyControl.Data;

namespace HandyControl.Controls
{
    public class Cascader : ComboBox
    {
        protected override bool IsItemItsOwnContainerOverride(object item) => item is CascaderItem;

        protected override DependencyObject GetContainerForItemOverride() => new CascaderItem();

        public static readonly DependencyProperty ExpandTriggerProperty = DependencyProperty.Register(
            "ExpandTrigger", typeof(ExpandTriggerType), typeof(Cascader), new PropertyMetadata(default(ExpandTriggerType)));

        public ExpandTriggerType ExpandTrigger
        {
            get => (ExpandTriggerType) GetValue(ExpandTriggerProperty);
            set => SetValue(ExpandTriggerProperty, value);
        }


    }
}