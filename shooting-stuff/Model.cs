using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MathNet.Numerics.Interpolation;

namespace stuff_falling
{
    public static class Model {

        public enum Forces { Archimedes, Drag, Viscosity }

        public class Parameters
        {

            private List<double> heights = new List<double>() { 0, 50, 100, 200, 300, 500, 1000, 2000, 3000, 5000, 8000, 10000, 12000, 15000, 20000, 50000, 100000, 120000 };
            private List<double> densities = new List<double>() { 1.225, 1.219, 1.213, 1.202, 1.190, 1.167, 1.112, 1.007, 0.909, 0.736, 0.526, 0.414, 0.312, 0.195, 0.089, 1.027e-3, 5.550e-7, 2.440e-8 };

            public int Number;
            public List<Forces> Forces = new List<Forces>();
            public double Height { get; set; }
            public double Speed { get; set; }
            private double angle;
            public double AngleRad { get { return angle; } set { angle = value; } }
            public double AngleGrad { get { return angle * 180 / Math.PI; } set { angle = value * Math.PI / 180; } }
            public Vector SpeedVector { get { return new Vector(Speed * Math.Cos(AngleRad), Speed * Math.Sin(AngleRad)); } }
            public double EndTime { get; set; }
            public double SegmentCount { get; set; }
            public bool IsConstGravitationalAcceleration { get; set; } = false;
            public bool IsConstDensity { get; set; } = false;
            public double SphereRadius { get; set; }
            public double SphereMass { get; set; }
            public double ConstEnviromentDensity { get; set; }
            public Func<double, double> EnviromentDensity {
                get
                {
                    if (IsConstDensity)
                        return (y) => ConstEnviromentDensity;
                    var interpolate = CubicSpline.InterpolateAkima(heights, densities);
                    return (y) => interpolate.Interpolate(y);
                }
            }
            public double EnviromentViscosity { get; set; }
            public double SphereVolume { get { return 4.0 / 3.0 * Math.PI * Math.Pow(SphereRadius, 3); } }
            public double SphereDensity { get { return SphereMass / SphereVolume; } }
            public double CrossSectionArea { get { return Math.PI * Math.Pow(SphereRadius, 2); } }
            public Func<double, double> ArchimedesCoeff { get { return (y) => EnviromentDensity(y) / SphereDensity; } }
            public Func<double, double> DragCoeff { get { return (y) => EnviromentDensity(y) * CrossSectionArea; } }
            public Func<double, double> ViscosityCoeff { get { return (y) => 6 * Math.PI * EnviromentViscosity * EnviromentDensity(y) * SphereRadius; } }
            public double Shift { get; set; } = 0;

            override public string ToString() {
                //string result = $"Эксперимент №{Number + 1}:\n" +
                //                $"Начальные условия: y0={Height:N3}, v0={Speed:N3}\n" +
                //                $"Ускорение свободного падения: g={(IsConstGravitationalAcceleration ? "9.81" : "g(y)")}\n" +
                //                 "Действующие силы:\n" +
                //                 "1) Сила тяжести\n";
                //for (int i = 0; i < Forces.Count; ++i)
                //    switch(Forces[i])
                //    {
                //        case Model.Forces.Archimedes:
                //            result += $"{i + 2}) Сила Архимеда (kA={(ArchimedesCoeff / 1:N3}, ρ(тела)={SphereDensity:N3}, ρ(среды)={EnviromentDensity:N3}, V={SphereVolume:N3}, R={SphereRadius:N3})\n";
                //            break;
                //        case Model.Forces.Drag:
                //            result += $"{i + 2}) Сила трения (K2={(IsConstDensity ? (DragCoeff(0) / SphereMass).ToString("N3") : "K2(y)")}, k2={DragCoeff:N3}, m={SphereMass:N3}, S={CrossSectionArea:N3}, ρ(среды){EnviromentDensity:N3})\n";
                //            break;
                //        case Model.Forces.Viscosity:
                //            result += $"{i + 2}) Сила вязкого трения (K1={ViscosityCoeff / SphereMass:N3}, k1={ViscosityCoeff:N3}, m={SphereMass:N3}, C=2, R={SphereRadius:N3}, ρ(среды)={EnviromentDensity:N3}, вязкость={EnviromentViscosity:N3})\n";
                //            break;
                //        default:
                //            throw new Exception("How did you get here???");
                //    }
                //return result;
                return "=^._.^=\n";
            }
    }

        public class Result : EventArgs
        {
            public List<Vector> Coordinates = new List<Vector>();
            public List<Vector> Speed = new List<Vector>();
            public List<double> Time = new List<double>();
        }

        private static Func<Vector, Vector, Vector> GetFunc(Parameters parameters)
        {
            Func<double, double> gravity;
            if (parameters.IsConstGravitationalAcceleration)
                gravity = y => -9.81;
            else 
                gravity = y => -9.81 / Math.Pow(1 + y / 6371000, 2);
            Func<double, double> archimedes = (y) => 0;
            Func<Vector, double, Vector> drag = (v, y) => new Vector(0, 0);
            foreach (var force in parameters.Forces)
            {
                switch(force)
                {
                    case Forces.Archimedes:
                        if (parameters.IsConstGravitationalAcceleration)
                            archimedes = y => parameters.ArchimedesCoeff(y) * 9.81;
                        else 
                            archimedes = y => parameters.ArchimedesCoeff(y) * 9.81 / Math.Pow(1 - y / 6371000, 2);
                        break;
                    case Forces.Drag:
                        drag = (v, y) => -parameters.DragCoeff(y) / parameters.SphereMass * (v - new Vector(parameters.Shift, 0)).Length * (v - new Vector(parameters.Shift, 0));
                        break;
                    case Forces.Viscosity:
                        drag = (v, y) => -parameters.ViscosityCoeff(y) * (v - new Vector(parameters.Shift, 0)) / parameters.SphereMass;
                        break;
                    default:
                        throw new Exception("How did you get here?!?!?!?!");
                }
            }
            return (y, v) => new Vector(0, gravity(y.Y) + archimedes(y.Y)) + drag(v, y.Y);
        }

        public static event EventHandler<Result> CalculationCompleted;

        private static void Calculate(Parameters parameters)
        {
            var func = GetFunc(parameters);
            Result result = new Result();
            result.Coordinates.Add(new Vector(0, parameters.Height));
            result.Speed.Add(parameters.SpeedVector);
            result.Time.Add(0);
            double dt = parameters.EndTime / parameters.SegmentCount;
            bool onGround = false;
            for (int i = 1; i <= parameters.SegmentCount; ++i)
            {
                result.Time.Add(i * dt);
                if (onGround)
                {
                    result.Speed.Add(new Vector(0, 0));
                    result.Coordinates.Add(result.Coordinates.Last());
                    continue;
                }
                result.Speed.Add(result.Speed.Last() + dt * func(result.Coordinates.Last(), result.Speed.Last()));
                result.Coordinates.Add(result.Coordinates.Last() + dt * result.Speed.Last());
                if (result.Coordinates.Last().Y <= 0)
                {
                    onGround = true;
                    result.Coordinates[result.Coordinates.Count - 1] = new Vector(result.Coordinates.Last().X, 0);
                    result.Speed[result.Speed.Count - 1] = new Vector(0, 0);
                }
            }
            CalculationCompleted(null, result);        
        }

        public static void BeginCalculate(Parameters parameters) {
            Thread thred = new Thread(() => Calculate(parameters));
            thred.Start();
        }

    }
}
