﻿using System;

namespace Strict.Language.Expressions;

/// <summary>
/// Not is the only unary expression that needs an extra space, e.g. 5 is not 6. When using -number
/// it will fail to find this as an identifier and check if one of the supported unary one letter
/// expression was used and will do the minus operation directly there.
/// </summary>
public sealed class Not : MethodCall
{
	public Not(Expression right) : base(
		right.ReturnType.GetMethod(UnaryOperator.Not, Array.Empty<Expression>()), right) { }

	public override string ToString() => UnaryOperator.Not + " " + Instance!;

	public static Expression Parse(Body body, ReadOnlySpan<char> input, ShuntingYard postfixTokens)
	{
		var right = body.Method.ParseExpression(body, input[postfixTokens.Output.Pop()]);
		var operatorText = input[postfixTokens.Output.Pop()];
		return operatorText.Equals(UnaryOperator.Not, StringComparison.Ordinal)
			? new Not(right)
			: throw new MethodExpressionParser.InvalidOperatorHere(body, operatorText.ToString());
	}
}