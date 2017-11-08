using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Charts;
using MahApps.Metro.Controls;
using LiveCharts.Wpf.Charts.Base;
using System.Windows.Media.Animation;
using CsvHelper;
using Microsoft.Win32;

namespace stuff_falling
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Model.CalculationCompleted += OnCalculationComplete;
            DataContext = this;
            Update();
        }

        public DataTable Data { get; set; } = new DataTable();

        public List<string> Labels { get; set; } = new List<string>();

        public SeriesCollection HeightSeries { get; set; } = new SeriesCollection();
        public SeriesCollection SpeedSeries { get; set; } = new SeriesCollection();
        public SeriesCollection AccelerationSeries { get; set; } = new SeriesCollection();

        public List<Model.Parameters> Parameters { get; set; } = new List<Model.Parameters>();

        private List<Ellipse> Ellipsies = new List<Ellipse>();
        private List<DoubleAnimationUsingKeyFrames> Animations = new List<DoubleAnimationUsingKeyFrames>();

        private List<double> Times = new List<double>();

        private bool AddEllipse = true;
        private bool UpdateAnimation = true;
        private int colorIndex = 0;
        private bool DataChanged = true;

        private delegate void UpdateDelegate(Model.Result result);

        private void UpdateSeries(SeriesCollection series, List<double> values)
        {
            if (series.Count > 0)
                series.RemoveAt(series.Count - 1);
            series.Add(new LineSeries
            {
                Title = "Эксперимент №" + (colorIndex + 1).ToString(),
                Values = new ChartValues<double>(values),
                LineSmoothness = 0,
                PointGeometry = null,
                Fill = new SolidColorBrush(),
                Stroke = new SolidColorBrush(Chart.Colors[(int)(colorIndex - Chart.Colors.Count * Math.Truncate(colorIndex / (double)Chart.Colors.Count))])
            });
        } 

        private void UpdateData(Model.Result result)
        {
            if (AddEllipse)
            {
                Ellipsies.Add(new Ellipse() { Width = 30, Height = 30 });
                Canvas.Children.Add(Ellipsies.Last());
                Canvas.SetLeft(Canvas.Children[Canvas.Children.Count - 1], 625);
                Canvas.SetTop(Canvas.Children[Canvas.Children.Count - 1], 575);
                Animations.Add(new DoubleAnimationUsingKeyFrames());
                AddEllipse = false;
            }
            UpdateSeries(HeightSeries, result.Height);
            UpdateSeries(SpeedSeries, result.Speed);
            UpdateSeries(AccelerationSeries, result.Acceleration);
            Labels.Clear();
            Labels.AddRange(result.Time.ConvertAll(new Converter<double, string>((double x) => { return x.ToString(); })));
            Ellipsies.Last().Fill = new SolidColorBrush(Chart.Colors[(int)(colorIndex - Chart.Colors.Count * Math.Truncate(colorIndex / (double)Chart.Colors.Count))]);
            UpdateAnimation = true;
            DataChanged = true;
            Times = result.Time;
            if (DataTab.IsSelected)
                UpdateDataTab();
            if (AnimationTab.IsSelected)
                SetAnim();
        }

        private void OnCalculationComplete(object sender, Model.Result result)
        {
            Dispatcher.Invoke(new UpdateDelegate(UpdateData), result);
        }

        private String PassDefaultIfEmpty(String s)
        {
            if (String.IsNullOrEmpty(s))
                return "1";
            if (s == "-" || s == "+")
                return s + "1";
            return s;
        }

        private void Update()
        {
            List<Model.Forces> forces = new List<Model.Forces>();
            if (Archimedes.IsChecked.Value)
                forces.Add(Model.Forces.Archimedes);
            if (LiquidFriction.IsChecked.Value)
                forces.Add(Model.Forces.Viscosity);
            if (GasDrag.IsChecked.Value)
                forces.Add(Model.Forces.Drag);
            Model.Parameters parameters = new Model.Parameters()
            {
                Number = colorIndex,
                Forces = forces,
                Height = Convert.ToDouble(PassDefaultIfEmpty(StartHeight.Text)),
                Speed = Convert.ToDouble(PassDefaultIfEmpty(StartSpeed.Text)),
                EndTime = Convert.ToDouble(PassDefaultIfEmpty(EndTime.Text)),
                SegmentCount = Convert.ToDouble(PassDefaultIfEmpty(PointNumber.Text)),
                IsConstGravitationalAcceleration = GIsConst.IsChecked.Value,
                SphereRadius = Convert.ToDouble(PassDefaultIfEmpty(BallRadius.Text)),
                SphereMass = Convert.ToDouble(PassDefaultIfEmpty(BallMass.Text)),
                EnviromentDensity = Convert.ToDouble(PassDefaultIfEmpty(EnvDensity.Text)),
                EnviromentViscosity = Convert.ToDouble(PassDefaultIfEmpty(EnvViscosity.Text))
            };
            if (Parameters.Count == 0)
                Parameters.Add(parameters);
            else
                Parameters[Parameters.Count - 1] = parameters;
            Model.BeginCalculate(parameters);
        }

        private void DoubleTBPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            System.Globalization.CultureInfo ci = System.Threading.Thread.CurrentThread.CurrentCulture;
            string decimalSeparator = ci.NumberFormat.NumberDecimalSeparator;
            if (decimalSeparator == ".")
            {
                decimalSeparator = "\\" + decimalSeparator;
            }
            var textBox = sender as TextBox;
            var pos = textBox.CaretIndex;
            e.Handled = !Regex.IsMatch(textBox.Text.Substring(0, pos) + e.Text + textBox.Text.Substring(pos), @"^[-+]?[0-9]*" + decimalSeparator + @"?[0-9]*$");
        }

        private void IntTBPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var pos = textBox.CaretIndex;
            e.Handled = !Regex.IsMatch(textBox.Text.Substring(0, pos) + e.Text + textBox.Text.Substring(pos), @"^[-+]?[0-9]*$");
        }

        private void TB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                Update();
            }
        }

        private void TB_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = e.Key == Key.Space;
        }

        private void CheckboxOnIsEnabledChanged(object sender, EventArgs e)
        {
            if (this.IsLoaded)
            {
                Update();
            }
        }

        private void ListBox_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement((ItemsControl)sender, (DependencyObject)e.OriginalSource) as ListBoxItem;
            if (item == null)
                return;
            var series = (LineSeries)item.Content;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                series.Visibility = series.Visibility == Visibility.Visible
                    ? Visibility.Hidden
                    : Visibility.Visible;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                int index = -1;
                if (HeightRadioButton.IsChecked.Value)
                    index = HeightSeries.IndexOf(series);
                else if (SpeedRadioButton.IsChecked.Value)
                    index = SpeedSeries.IndexOf(series);
                else
                    index = AccelerationSeries.IndexOf(series);
                if (index == HeightSeries.Count - 1)
                    return;
                HeightSeries.RemoveAt(index);
                SpeedSeries.RemoveAt(index);
                AccelerationSeries.RemoveAt(index);
                Canvas.Children.Remove(Ellipsies[index]);
                Ellipsies.RemoveAt(index);
                Animations.RemoveAt(index);
                Parameters.RemoveAt(index);
                UpdateAnimation = true;
                DataChanged = true;
            }
        }

        private void SaveExperimentButton_Click(object sender, RoutedEventArgs e)
        {
            HeightSeries.Insert(HeightSeries.Count - 1, HeightSeries.Last());
            SpeedSeries.Insert(SpeedSeries.Count - 1, SpeedSeries.Last());
            AccelerationSeries.Insert(AccelerationSeries.Count - 1, AccelerationSeries.Last());
            Parameters.Insert(Parameters.Count - 1, Parameters.Last());
            AddEllipse = true;
            ++colorIndex;
            Update();
        }

        private void SetAnim()
        {
            double height = -1;
            foreach (var it in HeightSeries)
                foreach (double h in it.Values)
                    if (h > height)
                        height = h;
            for (int i = 0; i < Ellipsies.Count; ++i)
            {
                DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames();
                foreach (double it in HeightSeries[i].Values)
                    anim.KeyFrames.Add(new LinearDoubleKeyFrame(575 - 460 * it / height));
                anim.Duration = new Duration(new TimeSpan(0, 0, 5));
                if (i == 0)
                    anim.Completed += AnimEnd;
                Animations[i] = anim;
                Canvas.SetTop(Ellipsies[i], Animations[i].KeyFrames[0].Value);
            }
            UpdateAnimation = false;
        }

        private void AnimEnd(object sender, EventArgs e)
        {
            StartAnimationButton.IsEnabled = true;
            foreach (var it in Ellipsies)
                it.BeginAnimation(Canvas.TopProperty, null);
        }

        private void StartAnimationButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            if (UpdateAnimation)
                SetAnim();
            for (int i = 0; i < Ellipsies.Count; ++i)
                Ellipsies[i].BeginAnimation(Canvas.TopProperty, Animations[i]);
            button.IsEnabled = false;
        }

        private void TRTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            HeightSeries.Clear();
            SpeedSeries.Clear();
            AccelerationSeries.Clear();
            for (int i = 0; i < Ellipsies.Count(); ++i)
                Canvas.Children.RemoveAt(Canvas.Children.Count - 1);
            Ellipsies.Clear();
            Animations.Clear();
            AddEllipse = true;
            colorIndex = 0;
            TB_TextChanged(sender, e);
        }

        private void UpdateDataTab()
        {
            if (!DataChanged)
                return;
            DataChanged = false;
            Data.Clear();
            Data.Columns.Clear();
            Data.Columns.Add("k");
            Data.Columns.Add("t__k");
            for (var col = 0; col < HeightSeries.Count; ++col)
            {
                Data.Columns.Add($"y__{Parameters[col].Number + 1}");
                Data.Columns.Add($"v__{Parameters[col].Number + 1}");
                Data.Columns.Add($"a__{Parameters[col].Number + 1}");
            }
            for (var row = 0; row < HeightSeries[0].ActualValues.Count; ++row)
            {
                List<object> temp = new List<object>()
                    {
                        row,
                        Times[row],
                    };
                for (var col = 0; col < HeightSeries.Count; ++col)
                {
                    temp.AddRange(new[] {
                            $"{HeightSeries[col].Values[row]:N3}",
                            $"{SpeedSeries[col].Values[row]:N3}",
                            $"{AccelerationSeries[col].Values[row]:N3}",
                        });
                }
                Data.Rows.Add(temp.ToArray());
            }
            Grid.ItemsSource = null;
            Grid.ItemsSource = Data.AsDataView();
            ExperimentList.ItemsSource = null;
            ExperimentList.ItemsSource = Parameters;
        }

        private void TabablzControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataTab.IsSelected)
                UpdateDataTab();
            if (AnimationTab.IsSelected)
                SetAnim();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV (*.csv)|*.csv";
            if (saveFileDialog.ShowDialog() == true)
            {
                StreamWriter file = new StreamWriter(saveFileDialog.FileName);
                var csv = new CsvWriter(file);
                foreach (DataColumn col in Data.Columns)
                {
                    csv.WriteField(col.ColumnName);
                }
                csv.NextRecord();
                foreach (DataRow row in Data.Rows)
                {
                    for (var i = 0; i < Data.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    csv.NextRecord();
                }
            }
        }
    }

    [ValueConversion(typeof(object), typeof(string))]
    public class StringFormatConverter : IValueConverter, IMultiValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(new object[] { value }, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Trace.TraceError("StringFormatConverter: does not support TwoWay or OneWayToSource bindings.");
            return DependencyProperty.UnsetValue;
        }

        public virtual object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string format = parameter?.ToString();
                if (String.IsNullOrEmpty(format))
                {
                    System.Text.StringBuilder builder = new System.Text.StringBuilder();
                    for (int index = 0; index < values.Length; ++index)
                    {
                        builder.Append("{" + index + "}");
                    }
                    format = builder.ToString();
                }
                return String.Format(/*culture,*/ format, values);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("StringFormatConverter({0}): {1}", parameter, ex.Message);
                return DependencyProperty.UnsetValue;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Trace.TraceError("StringFormatConverter: does not support TwoWay or OneWayToSource bindings.");
            return null;
        }
    }

    [ValueConversion(typeof(object), typeof(string))]
    public class RadiusToVolumeConverter : StringFormatConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double r;
            try
            {
                r = Double.Parse(value.ToString());
            }
            catch (FormatException e)
            {
                r = 1;
            }
            double v = Math.Pow(r, 3) * Math.PI * 4.0 / 3.0;
            return base.Convert(new object[] { v }, targetType, parameter, culture);
        }
    }

    [ValueConversion(typeof(object), typeof(string))]
    public class RadiusMassToDensityConverter : StringFormatConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double r;
            try
            {
                r = Double.Parse(values[0].ToString());
            }
            catch (FormatException e)
            {
                r = 1;
            }
            double v = Math.Pow(r, 3) * Math.PI * 4.0 / 3.0;
            double m;
            try
            {
                m = Double.Parse(values[1].ToString());
            }
            catch (FormatException e)
            {
                m = 1;
            }
            return base.Convert(new object[] { m / v }, targetType, parameter, culture);
        }
    }

    public class OpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible
                ? 1d
                : .2d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
