using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stuff_falling
{
    public static class Model {

        public enum Forces { Archimedes, Drag, Viscosity }

        public class Parameters
        {
            public int Number;
            public List<Forces> Forces = new List<Forces>();
            public double Height { get; set; }
            public double Speed { get; set; }
            public double EndTime { get; set; }
            public double SegmentCount { get; set; }
            public bool IsConstGravitationalAcceleration { get; set; }
            public double SphereRadius { get; set; }
            public double SphereMass { get; set; }
            public double EnviromentDensity { get; set; }
            public double EnviromentViscosity { get; set; }
            public double SphereVolume { get { return 4.0 / 3.0 * Math.PI * Math.Pow(SphereRadius, 3); } }
            public double SphereDensity { get { return SphereMass / SphereVolume; } }
            public double CrossSectionArea { get { return Math.PI * Math.Pow(SphereRadius, 2); } }
            public double ArchimedesCoeff { get { return EnviromentDensity / SphereDensity; } }
            public double DragCoeff { get { return EnviromentDensity * CrossSectionArea; } }
            public double ViscosityCoeff { get { return 6 * Math.PI * EnviromentViscosity * EnviromentDensity * SphereRadius; } }

            override public string ToString() {
                string result = $"Эксперимент №{Number + 1}:\n" +
                                $"Начальные условия: y0={Height:N3}, v0={Speed:N3}\n" +
                                $"Ускорение свободного падения: g={(IsConstGravitationalAcceleration ? "9.81" : "g(y)")}\n" +
                                 "Действующие силы:\n" +
                                 "1) Сила тяжести\n";
                for (int i = 0; i < Forces.Count; ++i)
                    switch(Forces[i])
                    {
                        case Model.Forces.Archimedes:
                            result += $"{i + 2}) Сила Архимеда (kA={ArchimedesCoeff:N3}, ρ(тела)={SphereDensity:N3}, ρ(среды)={EnviromentDensity:N3}, V={SphereVolume:N3}, R={SphereRadius:N3})\n";
                            break;
                        case Model.Forces.Drag:
                            result += $"{i + 2}) Сила трения (K2={DragCoeff / SphereMass:N3}, k2={DragCoeff:N3}, m={SphereMass:N3}, S={CrossSectionArea:N3}, ρ(среды){EnviromentDensity:N3})\n";
                            break;
                        case Model.Forces.Viscosity:
                            result += $"{i + 2}) Сила вязкого трения (K1={ViscosityCoeff / SphereMass:N3}, k1={ViscosityCoeff:N3}, m={SphereMass:N3}, C=2, R={SphereRadius:N3}, ρ(среды)={EnviromentDensity:N3}, вязкость={EnviromentViscosity:N3})\n";
                            break;
                        default:
                            throw new Exception("How did you get here???");
                    }
                return result;
            }
    }

        public class Result : EventArgs
        {
            public List<double> Height = new List<double>();
            public List<double> Speed = new List<double>();
            public List<double> Acceleration = new List<double>();
            public List<double> Time = new List<double>();
        }

        private static Func<double, double, double> GetFunc(Parameters parameters)
        {
            Func<double, double> gravity;
            if (parameters.IsConstGravitationalAcceleration)
                gravity = y => -9.81;
            else 
                gravity = y => -9.81 / Math.Pow(1 + y / 6371000, 2);
            Func<double, double> archimedes = (y) => 0;
            Func<double, double> drag = (v) => 0;
            foreach (var force in parameters.Forces)
            {
                switch(force)
                {
                    case Forces.Archimedes:
                        if (parameters.IsConstGravitationalAcceleration)
                            archimedes = y => parameters.ArchimedesCoeff * 9.81;
                        else 
                            archimedes = y => parameters.ArchimedesCoeff * 9.81 / Math.Pow(1 - y / 6371000, 2);
                        break;
                    case Forces.Drag:
                        drag = v => parameters.DragCoeff * v * v / parameters.SphereMass;
                        break;
                    case Forces.Viscosity:
                        drag = v => parameters.ViscosityCoeff * v / parameters.SphereMass;
                        break;
                    default:
                        throw new Exception("How did you get here?!?!?!?!");
                }
            }
            return (y, v) => gravity(y) + archimedes(y) + drag(v);
        }

        public static event EventHandler<Result> CalculationCompleted;

        private static void Calculate(Parameters parameters)
        {
            Func<double, double, double> func = GetFunc(parameters);
            Result result = new Result();
            result.Height.Add(parameters.Height);
            result.Speed.Add(parameters.Speed);
            result.Time.Add(0);
            double dt = parameters.EndTime / parameters.SegmentCount;
            bool onGround = false;
            for (int i = 1; i <= parameters.SegmentCount; ++i)
            {
                result.Time.Add(i * dt);
                if (onGround)
                {
                    result.Speed.Add(0);
                    result.Height.Add(0);
                    result.Acceleration.Add(0);
                    continue;
                }
                result.Speed.Add(result.Speed.Last() + dt * func(result.Height.Last(), result.Speed.Last()));
                result.Height.Add(result.Height.Last() + dt * result.Speed.Last());
                result.Acceleration.Add((result.Speed.Last() - result.Speed[result.Speed.Count - 2]) / dt);
                if (result.Height.Last() <= 0)
                {
                    onGround = true;
                    result.Height[result.Height.Count - 1] = 0;
                    result.Speed[result.Speed.Count - 1] = 0;
                }
            }
            result.Acceleration.Add(result.Acceleration.Last());
            CalculationCompleted(null, result);        
        }

        public static void BeginCalculate(Parameters parameters) {
            Thread thred = new Thread(() => Calculate(parameters));
            thred.Start();
        }

    }
}
