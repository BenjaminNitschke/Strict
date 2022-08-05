﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Strict.Language;

/// <summary>
/// .strict files contain a type or trait and must be in the correct namespace folder.
/// Strict code only contains optionally implement, then has*, then methods*. No empty lines.
/// There is no typical lexing/scoping/token splitting needed as Strict syntax is very strict.
/// </summary>
// ReSharper disable once HollowTypeName
public class Type : Context
{
	public Type(Package package, FileData file, ExpressionParser expressionParser) : base(package, file.Name)
	{
		if (package.FindDirectType(Name) != null)
			throw new TypeAlreadyExistsInPackage(Name, package);
		package.Add(this);
		this.expressionParser = expressionParser;
		for (lineNumber = 0; lineNumber < file.Lines.Length; lineNumber++)
			TryParseLine(file.Lines[lineNumber], file.Lines);
		if (Name != Base.None && Name != Base.Boolean && //TODO: hack to make Root dummy parsing work!
			methods.Count == 0 && members.Count + implements.Count < 2)
			throw new NoMethodsFound(this, lineNumber);
		foreach (var trait in implements)
			if (trait.IsTrait)
				CheckIfTraitIsImplemented(trait);
	}

	public sealed class TypeAlreadyExistsInPackage : Exception
	{
		public TypeAlreadyExistsInPackage(string name, Package package) : base(
			name + " in package: " + package) { }
	}

	private readonly ExpressionParser expressionParser;
	private int lineNumber;
	public string FilePath => Path.Combine(Package.FolderPath, Name) + Extension;
	public Package Package => (Package)Parent;

	private void CheckIfTraitIsImplemented(Type trait)
	{
		var nonImplementedTraitMethods = trait.Methods.
			Where(traitMethod => traitMethod.Name != Method.From &&
				methods.All(implementedMethod => traitMethod.Name != implementedMethod.Name)).ToList();
		if (nonImplementedTraitMethods.Count > 0)
			throw new MustImplementAllTraitMethods(this, nonImplementedTraitMethods);
	}

	private void TryParseLine(string line, string[] lines)
	{
		try
		{
			ParseLine(line, lines);
		}
		catch (ParsingFailed)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new ParsingFailed(this, lineNumber, line, ex);
		}
	}

	private void ParseLine(string line, string[] lines)
	{
		var words = ParseWords(line);
		if (words[0] == Import)
			imports.Add(ParseImport(words));
		else if (words[0] == Implement)
			implements.Add(ParseImplement(words));
		else if (words[0] == Has)
			members.Add(ParseMember(line));
		else
			methods.Add(new Method(this, lineNumber, expressionParser, GetAllMethodLines(line, lines)));
	}

	private Package ParseImport(IReadOnlyList<string> words)
	{
		if (implements.Count > 0 || members.Count > 0 || methods.Count > 0)
			throw new ImportMustBeFirst(words[1]);
		var import = Package.Find(words[1]);
		if (import == null)
			throw new PackageNotFound(words[1]);
		return import;
	}

	public sealed class ImportMustBeFirst : Exception
	{
		public ImportMustBeFirst(string package) : base(package) { }
	}

	public sealed class PackageNotFound : Exception
	{
		public PackageNotFound(string package) : base(package) { }
	}

	private Type ParseImplement(IReadOnlyList<string> words)
	{
		if (members.Count > 0 || methods.Count > 0)
			throw new ImplementMustComeBeforeMembersAndMethods(words[1]);
		if (words[1] == "Any")
			throw new ImplementAnyIsImplicitAndNotAllowed();
		return Package.GetType(words[1]);
	}

	public sealed class ImplementMustComeBeforeMembersAndMethods : Exception
	{
		public ImplementMustComeBeforeMembersAndMethods(string type) : base(type) { }
	}

	public sealed class ImplementAnyIsImplicitAndNotAllowed : Exception { }

	private Member ParseMember(string line)
	{
		if (methods.Count > 0)
			throw new MembersMustComeBeforeMethods(line);
		var nameAndExpression = line[(Has.Length + 1)..].Split(" = ");
		var expression = nameAndExpression.Length > 1
			? expressionParser.ParseAssignmentExpression(new Member(this, nameAndExpression[0], null).Type,
				nameAndExpression[1], lineNumber)
			: null;
		return new Member(this, nameAndExpression[0], expression);
	}

	public sealed class MembersMustComeBeforeMethods : Exception
	{
		public MembersMustComeBeforeMethods(string line) : base(line) { }
	}

	public const string Implement = "implement";
	public const string Import = "import";
	public const string Has = "has";

	private string[] ParseWords(string line)
	{
		if (line.Length != line.TrimStart().Length)
			throw new ExtraWhitespacesFoundAtBeginningOfLine(this, lineNumber, line);
		if (line.Length != line.TrimEnd().Length)
			throw new ExtraWhitespacesFoundAtEndOfLine(this, lineNumber, line);
		if (line.Length == 0)
			throw new EmptyLineIsNotAllowed(this, lineNumber);
		return line.SplitWords();
	}

	public sealed class ExtraWhitespacesFoundAtBeginningOfLine : ParsingFailed
	{
		public ExtraWhitespacesFoundAtBeginningOfLine(Type type, int lineNumber, string message,
			string method = "") : base(type, lineNumber, message, method) { }
	}

	public sealed class ExtraWhitespacesFoundAtEndOfLine : ParsingFailed
	{
		public ExtraWhitespacesFoundAtEndOfLine(Type type, int lineNumber, string message,
			string method = "") : base(type, lineNumber, message, method) { }
	}

	public sealed class EmptyLineIsNotAllowed : ParsingFailed
	{
		public EmptyLineIsNotAllowed(Type type, int lineNumber) : base(type, lineNumber) { }
	}

	public sealed class NoMethodsFound : ParsingFailed
	{
		public NoMethodsFound(Type type, int lineNumber) : base(type, lineNumber,
			"Each type must have at least one method, otherwise it is useless") { }
	}

	public sealed class MustImplementAllTraitMethods : ParsingFailed
	{
		public MustImplementAllTraitMethods(Type type, IEnumerable<Method> missingTraitMethods) :
			base(type, type.lineNumber, "Missing methods: " + string.Join(", ", missingTraitMethods)) { }
	}

	private string[] GetAllMethodLines(string definitionLine, string[] lines)
	{
		var methodLines = new List<string> { definitionLine };
		if (IsTrait && IsNextLineValidMethodBody(lines))
			throw new TypeHasNoMembersAndThusMustBeATraitWithoutMethodBodies(this);
		if (!IsTrait && !IsNextLineValidMethodBody(lines))
			throw new MethodMustBeImplementedInNonTraitType(this, definitionLine);
		while (IsNextLineValidMethodBody(lines))
			methodLines.Add(lines[++lineNumber]);
		return methodLines.ToArray(); //TODO: would be cool if this can be avoided
	}

	private bool IsNextLineValidMethodBody(string[] lines)
	{
		if (lineNumber + 1 >= lines.Length)
			return false;
		var line = lines[lineNumber + 1];
		if (line.StartsWith('\t'))
			return true;
		if (line.Length != line.TrimStart().Length)
			throw new ExtraWhitespacesFoundAtBeginningOfLine(this, lineNumber, line);
		return false;
	}

	public sealed class TypeHasNoMembersAndThusMustBeATraitWithoutMethodBodies : ParsingFailed
	{
		public TypeHasNoMembersAndThusMustBeATraitWithoutMethodBodies(Type type) : base(type, 0) { }
	}

	// ReSharper disable once HollowTypeName
	public sealed class MethodMustBeImplementedInNonTraitType : ParsingFailed
	{
		public MethodMustBeImplementedInNonTraitType(Type type, string definitionLine) : base(type,
			type.lineNumber, definitionLine) { }
	}

	public IReadOnlyList<Type> Implements => implements;
	private readonly List<Type> implements = new();
	public IReadOnlyList<Package> Imports => imports;
	private readonly List<Package> imports = new();
	public IReadOnlyList<Member> Members => members;
	private readonly List<Member> members = new();
	public IReadOnlyList<Method> Methods => methods;
	protected readonly List<Method> methods = new();
	public bool IsTrait => Implements.Count == 0 && Members.Count == 0 && Name != Base.Number;

	public override string ToString() =>
		base.ToString() + (implements.Count > 0
			? " " + nameof(Implements) + " " + Implements.ToWordList()
			: "");

	//https://deltaengine.fogbugz.com/f/cases/24806

	public override Type? FindType(string name, Context? searchingFrom = null) =>
		name == Name || name.Contains('.') && name == base.ToString()
			? this
			: Package.FindType(name, searchingFrom ?? this);

	private void CheckForFilePathErrors(string filePath, IReadOnlyList<string> paths, string directory)
	{
		if (directory.EndsWith(@"\strict-lang\Strict", StringComparison.Ordinal))
			throw new StrictFolderIsNotAllowedForRootUseBaseSubFolder(filePath); //ncrunch: no coverage
	}

	//ncrunch: no coverage start, tests too flacky when creating and deleting wrong file
	public sealed class StrictFolderIsNotAllowedForRootUseBaseSubFolder : Exception
	{
		public StrictFolderIsNotAllowedForRootUseBaseSubFolder(string filePath) : base(filePath) { }
	} //ncrunch: no coverage end

	public const string Extension = ".strict";

	public Method GetMethod(string methodName, IReadOnlyList<Expression> arguments) =>
		FindMethod(methodName, arguments) ?? (methodName == Method.From && arguments.Count == 0
			? throw new StaticMethodCallsAreNotPossible(this)
			: throw new NoMatchingMethodFound(this, methodName, AvailableMethods));

	public sealed class StaticMethodCallsAreNotPossible : Exception
	{
		public StaticMethodCallsAreNotPossible(Type type) : base(type.ToString()) { }
	}

	public Method? FindMethod(string methodName, IReadOnlyList<Expression> arguments)
	{
		if (!AvailableMethods.TryGetValue(methodName, out var matchingMethods))
			return null;
		var bestMatch = matchingMethods[0];
		foreach (var method in matchingMethods)
			if (method.Parameters.Count == arguments.Count)
			{
				var doAllParameterTypesMatch = true;
				for (var index = 0; index < method.Parameters.Count; index++)
					if (!arguments[index].ReturnType.IsCompatible(method.Parameters[index].Type))
					{
						doAllParameterTypesMatch = false;
						break;
					}
				if (doAllParameterTypesMatch)
					return method;
				bestMatch = method;
			}
		throw new ArgumentsDoNotMatchMethodParameters(arguments, bestMatch);
	}

	private bool IsCompatible(Type sameOrBaseType) =>
		this == sameOrBaseType || sameOrBaseType.Name == Base.Any ||
		implements.Contains(sameOrBaseType) || CanUpCast(sameOrBaseType);

	private bool CanUpCast(Type sameOrBaseType)
	{
		if (sameOrBaseType.Name is Base.List)
			return Name == Base.Number || implements.Contains(GetType(Base.Number)) || Name == Base.Text; //TODO: check actual generics of this list type!
		if (sameOrBaseType.Name is Base.Text or Base.List)
			return Name == Base.Number || implements.Contains(GetType(Base.Number));
		return false;
	}

	/// <summary>
	/// Builds dictionary the first time we use it to access any method of this type or any of the
	/// implements parent types recursively. Filtering has to be done by <see cref="FindMethod"/>
	/// </summary>
	public IReadOnlyDictionary<string, List<Method>> AvailableMethods
	{
		get
		{
			if (cachedAvailableMethods != null)
				return cachedAvailableMethods;
			cachedAvailableMethods = new Dictionary<string, List<Method>>();
			foreach (var method in methods)
				if (cachedAvailableMethods.ContainsKey(method.Name))
					cachedAvailableMethods[method.Name].Add(method);
				else
					cachedAvailableMethods.Add(method.Name, new List<Method> { method });
			foreach (var implementType in implements)
				AddAvailableMethods(implementType);
			if (Name != Base.Any)
				AddAvailableMethods(GetType(Base.Any));
			return cachedAvailableMethods;
		}
	}

	private void AddAvailableMethods(Type implementType)
	{
		foreach (var (methodName, otherMethods) in implementType.AvailableMethods)
			if (cachedAvailableMethods!.ContainsKey(methodName))
				cachedAvailableMethods[methodName].AddRange(otherMethods);
			else
				cachedAvailableMethods.Add(methodName, otherMethods);
	}

	private Dictionary<string, List<Method>>? cachedAvailableMethods;

	public class NoMatchingMethodFound : Exception
	{
		public NoMatchingMethodFound(Type type, string methodName,
			IReadOnlyDictionary<string, List<Method>> availableMethods) : base(methodName +
			" not found for " + type + ", available methods: " + availableMethods.Keys.ToWordList()) { }
	}

	public sealed class ArgumentsDoNotMatchMethodParameters : Exception
	{
		public ArgumentsDoNotMatchMethodParameters(IReadOnlyList<Expression> arguments, Method method)
			: base((arguments.Count == 0
					? "No arguments does "
					: "Arguments: " + arguments.Select(a => a.ReturnType + " " + a).ToWordList() + " do ") +
				"not match \"" + method.Type + "." + method.Name + "\" method parameters: " +
				method.Parameters.ToBrackets()) { }
	}
}

public record FileData(string Name, string[] Lines);