using System;
using Xunit;
using Jace;

namespace AAEmu.UnitTests
{
    public class FormulaManagerTests
    {
        private readonly CalculationEngine _engine;

        public FormulaManagerTests()
        {
            _engine = new CalculationEngine();
            _engine.AddFunction("clamp", (a, b, c) => Math.Min(Math.Max(a, b), c));
            _engine.AddFunction("if_negative", (a, b, c) => a < 0 ? b : c);
            _engine.AddFunction("if_positive", (a, b, c) => a > 0 ? b : c);
            _engine.AddFunction("if_zero", (a, b, c) => a == 0 ? b : c);
            _engine.AddFunction("min", (a, b) => Math.Min(a, b));
            _engine.AddFunction("max", (a, b) => Math.Max(a, b));
            _engine.AddFunction("floor", (a) => Math.Floor(a));
            _engine.AddFunction("sqrt", (a) => Math.Sqrt(a));
            _engine.AddFunction("log", (a) => Math.Log(a));
        }

        [Fact]
        public void Clamp_ShouldClampValue()
        {
            Assert.Equal(5, _engine.Calculate("clamp(5, 0, 10)"));
            Assert.Equal(0, _engine.Calculate("clamp(-1, 0, 10)"));
            Assert.Equal(10, _engine.Calculate("clamp(15, 0, 10)"));
        }

        [Fact]
        public void IfNegative_ShouldReturnCorrectBranch()
        {
            Assert.Equal(1, _engine.Calculate("if_negative(-5, 1, 0)"));
            Assert.Equal(0, _engine.Calculate("if_negative(5, 1, 0)"));
        }

        [Fact]
        public void IfPositive_ShouldReturnCorrectBranch()
        {
            Assert.Equal(1, _engine.Calculate("if_positive(5, 1, 0)"));
            Assert.Equal(0, _engine.Calculate("if_positive(-5, 1, 0)"));
        }

        [Fact]
        public void IfZero_ShouldReturnCorrectBranch()
        {
            Assert.Equal(1, _engine.Calculate("if_zero(0, 1, 0)"));
            Assert.Equal(0, _engine.Calculate("if_zero(5, 1, 0)"));
        }

        [Fact]
        public void Min_ShouldReturnMinimum()
        {
            Assert.Equal(3, _engine.Calculate("min(3, 7)"));
            Assert.Equal(-1, _engine.Calculate("min(-1, 5)"));
        }

        [Fact]
        public void Max_ShouldReturnMaximum()
        {
            Assert.Equal(7, _engine.Calculate("max(3, 7)"));
            Assert.Equal(5, _engine.Calculate("max(-1, 5)"));
        }

        [Fact]
        public void Floor_ShouldFloorValue()
        {
            Assert.Equal(3, _engine.Calculate("floor(3.7)"));
            Assert.Equal(-4, _engine.Calculate("floor(-3.2)"));
        }

        [Fact]
        public void Sqrt_ShouldCalculateSquareRoot()
        {
            Assert.Equal(3, _engine.Calculate("sqrt(9)"));
            Assert.Equal(5, _engine.Calculate("sqrt(25)"));
        }
    }
}
