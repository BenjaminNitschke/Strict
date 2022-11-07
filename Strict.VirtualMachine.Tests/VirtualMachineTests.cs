using NUnit.Framework;

namespace Strict.VirtualMachine.Tests;

public sealed class VirtualMachineTests : BaseVirtualMachineTests
{
	[SetUp]
	public void Setup() => vm = new VirtualMachine();

	private VirtualMachine vm = null!;

	[TestCase(Instruction.Add, 15, 5, 10)]
	[TestCase(Instruction.Subtract, 5, 8, 3)]
	[TestCase(Instruction.Multiply, 4, 2, 2)]
	[TestCase(Instruction.Divide, 3, 7.5, 2.5)]
	[TestCase(Instruction.Add, "105", "5", 10)]
	[TestCase(Instruction.Add, "510", 5, "10")]
	[TestCase(Instruction.Add, "510", "5", "10")]
	public void Execute(Instruction operation, object expected, params object[] inputs) =>
		Assert.That(vm.Execute(BuildStatements(inputs, operation)).Registers[Register.R1].Value,
			Is.EqualTo(expected));

	private static Statement[]
		BuildStatements(IReadOnlyList<object> inputs, Instruction operation) =>
		new Statement[]
		{
			new(Instruction.Set, new Instance(inputs[0] is int
				? NumberType
				: TextType, inputs[0]), Register.R0),
			new(Instruction.Set, new Instance(inputs[1] is int
				? NumberType
				: TextType, inputs[1]), Register.R1),
			new(operation, Register.R0, Register.R1)
		};

	[Test]
	public void LoadVariable() =>
		Assert.That(
			vm.Execute(new Statement[]
			{
				new LoadConstantStatement(Register.R0, new Instance(NumberType, 5))
			}).Registers[Register.R0].Value, Is.EqualTo(5));

	[Test]
	public void SetAndAdd() =>
		Assert.That(vm.Execute(new Statement[]
		{
			new LoadConstantStatement(Register.R0, new Instance(NumberType, 10)),
			new LoadConstantStatement(Register.R1, new Instance(NumberType, 5)),
			new(Instruction.Add, Register.R0, Register.R1, Register.R2)
		}).Registers[Register.R2].Value, Is.EqualTo(15));

	[Test]
	public void AddFiveTimes() =>
		Assert.That(vm.Execute(new Statement[]
		{
			new(Instruction.Set, new Instance(NumberType, 5), Register.R0),
			new(Instruction.Set, new Instance(NumberType, 1), Register.R1),
			new(Instruction.Set, new Instance(NumberType, 0), Register.R2),
			new(Instruction.Add, Register.R0, Register.R2, Register.R2), // R2 = R0 + R2
			new(Instruction.Subtract, Register.R0, Register.R1, Register.R0),
			new JumpStatement(Instruction.JumpIfNotZero, -3)
		}).Registers[Register.R2].Value, Is.EqualTo(0 + 5 + 4 + 3 + 2 + 1));

	[TestCase("ArithmeticFunction(10, 5).Calculate(\"add\")", 15)]
	[TestCase("ArithmeticFunction(10, 5).Calculate(\"subtract\")", 5)]
	[TestCase("ArithmeticFunction(10, 5).Calculate(\"multiply\")", 50)]
	[TestCase("ArithmeticFunction(10, 5).Calculate(\"divide\")", 2)]
	public void RunArithmeticFunctionExample(string methodCall, int expectedResult)
	{
		var statements = new ByteCodeGenerator(GenerateMethodCallFromSource("ArithmeticFunction",
			methodCall, ArithmeticFunctionExample)).Generate();
		Assert.That(vm.Execute(statements).Returns?.Value, Is.EqualTo(expectedResult));
	}

	[Test]
	public void ConditionalJump() =>
		Assert.That(
			vm.Execute(new Statement[]
			{
				new(Instruction.Set, new Instance(NumberType, 5), Register.R0),
				new(Instruction.Set, new Instance(NumberType, 1), Register.R1),
				new(Instruction.Set, new Instance(NumberType, 10), Register.R2),
				new(Instruction.LessThan, Register.R2, Register.R0),
				new JumpStatement(Instruction.JumpIfTrue, 2),
				new(Instruction.Add, Register.R2, Register.R0, Register.R0)
			}).Registers[Register.R0].Value, Is.EqualTo(15));

	[TestCase(Instruction.GreaterThan, new[] { 1, 2 }, 2 - 1)]
	[TestCase(Instruction.LessThan, new[] { 1, 2 }, 1 + 2)]
	[TestCase(Instruction.Equal, new[] { 5, 5 }, 5 + 5)]
	[TestCase(Instruction.NotEqual, new[] { 5, 5 }, 5 - 5)]
	public void ConditionalJumpIfAndElse(Instruction conditional, int[] registers, int expected) =>
		Assert.That(vm.Execute(new Statement[]
		{
			new(Instruction.Set, new Instance(NumberType, registers[0]), Register.R0),
			new(Instruction.Set, new Instance(NumberType, registers[1]), Register.R1),
			new(conditional, Register.R0, Register.R1),
			new JumpStatement(Instruction.JumpIfTrue, 2),
			new(Instruction.Subtract, Register.R1, Register.R0, Register.R0),
			new JumpStatement(Instruction.JumpIfFalse, 2),
			new(Instruction.Add, Register.R0, Register.R1, Register.R0)
		}).Registers[Register.R0].Value, Is.EqualTo(expected));

	[TestCase(Instruction.Add)]
	[TestCase(Instruction.GreaterThan)]
	public void OperandsRequired(Instruction instruction) =>
		Assert.That(() => vm.Execute(new Statement[] { new(instruction, Register.R0) }),
			Throws.InstanceOf<VirtualMachine.OperandsRequired>());
}