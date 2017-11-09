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

        public SeriesCollection YSeries { get; set; } = new SeriesCollection();
        public SeriesCollection XSeries { get; set; } = new SeriesCollection();
        public SeriesCollection YSpeedSeries { get; set; } = new SeriesCollection();
        public SeriesCollection XSpeedSeries { get; set; } = new SeriesCollection();

        public List<Model.Parameters> Parameters { get; set; } = new List<Model.Parameters>();

        private List<Ellipse> Ellipsies = new List<Ellipse>();
        private List<Polyline> Polylines = new List<Polyline>();
        private List<DoubleAnimationUsingKeyFrames> YAnimations = new List<DoubleAnimationUsingKeyFrames>();
        private List<DoubleAnimationUsingKeyFrames> XAnimations = new List<DoubleAnimationUsingKeyFrames>();

        private List<double> Times = new List<double>();

        private bool AddEllipse = true;
        private bool UpdateAnimation = true;
        private int colorIndex = 0;
        private bool DataChanged = true;

        private delegate void UpdateDelegate(Model.Result result);

        private void UpdateSeries(SeriesCollection series, List<Vector> values, Func<Vector, double> get)
        {
            if (series.Count > 0)
                series.RemoveAt(series.Count - 1);
            series.Add(new LineSeries
            {
                Title = "Эксперимент №" + (colorIndex + 1).ToString(),
                Values = new ChartValues<double>(values.ConvertAll(new Converter<Vector, double>(x => get(x)))),
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
                Polylines.Add(new Polyline
                {
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 5
                });
                AnimCanvas.Children.Add(Polylines.Last());
                Canvas.SetLeft(AnimCanvas.Children[AnimCanvas.Children.Count - 1], 200);
                Canvas.SetTop(AnimCanvas.Children[AnimCanvas.Children.Count - 1], 200);
                Ellipsies.Add(new Ellipse() { Width = 30, Height = 30 });
                AnimCanvas.Children.Add(Ellipsies.Last());
                Canvas.SetLeft(AnimCanvas.Children[AnimCanvas.Children.Count - 1], 625);
                Canvas.SetTop(AnimCanvas.Children[AnimCanvas.Children.Count - 1], 575);
                YAnimations.Add(new DoubleAnimationUsingKeyFrames());
                XAnimations.Add(new DoubleAnimationUsingKeyFrames());
                AddEllipse = false; 
            }
            UpdateSeries(YSeries, result.Coordinates, v => v.Y);
            UpdateSeries(XSeries, result.Coordinates, v => v.X);
            UpdateSeries(YSpeedSeries, result.Speed, v => v.Y);
            UpdateSeries(XSpeedSeries, result.Speed, v => v.X);
            Labels.Clear();
            Labels.AddRange(result.Time.ConvertAll(new Converter<double, string>((double x) => { return x.ToString(); })));
            Ellipsies.Last().Fill = new SolidColorBrush(Chart.Colors[(int)(colorIndex - Chart.Colors.Count * Math.Truncate(colorIndex / (double)Chart.Colors.Count))]);
            Polylines.Last().Points.Clear();
            foreach (var it in result.Coordinates)
                Polylines.Last().Points.Add(new Point(it.X, it.Y));
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
                ConstEnviromentDensity = Convert.ToDouble(PassDefaultIfEmpty(EnvDensity.Text)),
                EnviromentViscosity = Convert.ToDouble(PassDefaultIfEmpty(EnvViscosity.Text)),
                IsConstDensity = RhoIsConst.IsChecked.Value,
                Shift = Convert.ToDouble(PassDefaultIfEmpty(EnvSpeed.Text)),
                AngleGrad = Convert.ToDouble(PassDefaultIfEmpty(Angle.Text))
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
                if (YRadioButton.IsChecked.Value)
                    index = YSeries.IndexOf(series);
                else if (XRadioButton.IsChecked.Value)
                    index = XSeries.IndexOf(series);
                else if (YSpeedRadioButton.IsChecked.Value)
                    index = YSpeedSeries.IndexOf(series);
                else
                    index = XSpeedSeries.IndexOf(series);
                if (index == YSeries.Count - 1)
                    return;
                YSeries.RemoveAt(index);
                XSeries.RemoveAt(index);
                YSpeedSeries.RemoveAt(index);
                XSpeedSeries.RemoveAt(index);
                AnimCanvas.Children.Remove(Ellipsies[index]);
                Ellipsies.RemoveAt(index);
                YAnimations.RemoveAt(index);
                Parameters.RemoveAt(index);
                UpdateAnimation = true;
                DataChanged = true;
            }
        }

        private void SaveExperimentButton_Click(object sender, RoutedEventArgs e)
        {
            YSeries.Insert(YSeries.Count - 1, YSeries.Last());
            XSeries.Insert(XSeries.Count - 1, XSeries.Last());
            YSpeedSeries.Insert(YSpeedSeries.Count - 1, YSpeedSeries.Last());
            XSpeedSeries.Insert(XSpeedSeries.Count - 1, XSpeedSeries.Last());
            Parameters.Insert(Parameters.Count - 1, Parameters.Last());
            AddEllipse = true;
            ++colorIndex;
            Update();
        }

        private void SetAnim()
        {
            //TODO: MAKE NEW ANIMATION
            double height = -1;
            foreach (var it in YSeries)
                foreach (double h in it.Values)
                    if (h > height)
                        height = h;
            double left = Double.PositiveInfinity;
            double right = Double.NegativeInfinity;
            foreach (var it in XSeries)
                foreach (double x in it.Values) {
                    if (x < left)
                        left = x;
                    if (x > right)
                        right = x;
                }
            double kx = right != left ? (AnimCanvas.Width - 200) / (right - left) : 0;
            double ky = (AnimCanvas.Height - 200) / height;
            for (int i = 0; i < Ellipsies.Count; ++i)
            {
                DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames();
                foreach (double it in YSeries[i].Values)
                    anim.KeyFrames.Add(new LinearDoubleKeyFrame(AnimCanvas.Height - 100 -  ky * it));
                anim.Duration = new Duration(new TimeSpan(0, 0, 5));
                if (i == 0)
                    anim.Completed += AnimEnd;
                YAnimations[i] = anim;
                DoubleAnimationUsingKeyFrames anim1 = new DoubleAnimationUsingKeyFrames();
                foreach (double it in XSeries[i].Values)
                    anim1.KeyFrames.Add(new LinearDoubleKeyFrame((it - left) * kx + 100));
                anim1.Duration = new Duration(new TimeSpan(0, 0, 5));
                XAnimations[i] = anim1;
                Canvas.SetLeft(Polylines[i], XAnimations[i].KeyFrames[0].Value);
                Canvas.SetTop(Polylines[i], YAnimations[i].KeyFrames[0].Value);
                Canvas.SetTop(Ellipsies[i], YAnimations[i].KeyFrames[0].Value);
                Canvas.SetLeft(Ellipsies[i], XAnimations[i].KeyFrames[0].Value);
                Polylines[i].Points.Clear();
                for (int j = 0; j < YSeries[i].Values.Count; ++j)
                    Polylines.Last().Points.Add(new Point(anim1.KeyFrames[j].Value - anim1.KeyFrames[0].Value + 15, anim.KeyFrames[j].Value - anim.KeyFrames[0].Value + 15));
            }
            UpdateAnimation = false;
        }

        private void AnimEnd(object sender, EventArgs e)
        {
            StartAnimationButton.IsEnabled = true;
            foreach (var it in Ellipsies)
            {
                it.BeginAnimation(Canvas.TopProperty, null);
                it.BeginAnimation(Canvas.LeftProperty, null);
            }
        }

        private void StartAnimationButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            if (UpdateAnimation)
                SetAnim();
            for (int i = 0; i < Ellipsies.Count; ++i)
            {
                Ellipsies[i].BeginAnimation(Canvas.TopProperty, YAnimations[i]);
                Ellipsies[i].BeginAnimation(Canvas.LeftProperty, XAnimations[i]);
            }
            button.IsEnabled = false;
        }

        private void TRTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            YSeries.Clear();
            XSeries.Clear();
            YSpeedSeries.Clear();
            XSpeedSeries.Clear();
            for (int i = 0; i < Ellipsies.Count(); ++i)
                AnimCanvas.Children.RemoveAt(AnimCanvas.Children.Count - 1);
            Ellipsies.Clear();
            YAnimations.Clear();
            XAnimations.Clear();
            AddEllipse = true;
            colorIndex = 0;
            TB_TextChanged(sender, e);
        }

        private void UpdateDataTab()
        {
            //TODO: MAKE NEW TABLE???
            if (!DataChanged)
                return;
            DataChanged = false;
            Data.Clear();
            Data.Columns.Clear();
            Data.Columns.Add("k");
            Data.Columns.Add("t__k");
            for (var col = 0; col < YSeries.Count; ++col)
            {
                Data.Columns.Add($"x__{Parameters[col].Number + 1}");
                Data.Columns.Add($"y__{Parameters[col].Number + 1}");
                Data.Columns.Add($"v__x{Parameters[col].Number + 1}");
                Data.Columns.Add($"v__y{Parameters[col].Number + 1}");
            }
            for (var row = 0; row < YSeries[0].ActualValues.Count; ++row)
            {
                List<object> temp = new List<object>()
                    {
                        row,
                        Times[row],
                    };
                for (var col = 0; col < YSeries.Count; ++col)
                {
                    temp.AddRange(new[] {
                            $"{XSeries[col].Values[row]:N3}",
                            $"{YSeries[col].Values[row]:N3}",
                            $"{XSpeedSeries[col].Values[row]:N3}",
                            $"{YSpeedSeries[col].Values[row]:N3}",
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
