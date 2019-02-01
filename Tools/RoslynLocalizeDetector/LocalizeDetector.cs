using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynLocalizeDetector
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class LocalizeDetector : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "LocalizeAnalyzer";

		private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			DiagnosticId,
			"Title",
			"MessageFormat",
			"Category",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Description");

		private Action<string> localizeListener;

		public LocalizeDetector(Action<string> listener)
		{
			localizeListener = listener;
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
			context.RegisterSyntaxNodeAction(AnalyzeAttributeNodes, SyntaxKind.Attribute);
		}

		private void AnalyzeInvocation(SyntaxNodeAnalysisContext analysisContext)
		{
			var invocation = (InvocationExpressionSyntax)analysisContext.Node;

			var identifier = GetMethodCallIdentifier(invocation);
			if (identifier == null)
			{
				return;
			}

			if (identifier.Value.ValueText == "Localize")
			{
				if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
				{
					if (memberAccess.Expression is LiteralExpressionSyntax literalExpression)
					{
						var textInvokedOn = literalExpression.Token.ValueText;
						localizeListener.Invoke(textInvokedOn);
					}
				}
			}
		}

		protected SyntaxToken? GetMethodCallIdentifier(InvocationExpressionSyntax invocation)
		{
			var directMethodCall = invocation.Expression as IdentifierNameSyntax;
			if (directMethodCall != null)
			{
				return directMethodCall.Identifier;
			}

			var memberAccessCall = invocation.Expression as MemberAccessExpressionSyntax;
			if (memberAccessCall != null)
			{
				return memberAccessCall.Name.Identifier;
			}

			return null;
		}

		// Find DisplayName attributes and extract localization strings from them
		private void AnalyzeAttributeNodes(SyntaxNodeAnalysisContext analysisContext)
		{
			var node = analysisContext.Node;
			var attributSyntax = node as AttributeSyntax;
			var name = GetSimpleNameFromNode(attributSyntax);

			if ((name.Identifier.Value.ToString() == "DisplayName"
				|| name.Identifier.Value.ToString() == "Description")
				&& attributSyntax.ArgumentList.Arguments.FirstOrDefault() is AttributeArgumentSyntax attributeArgument
				&& attributeArgument.Expression is LiteralExpressionSyntax literalExpression)
			{
				var textInvokedOn = literalExpression.Token.ValueText;
				localizeListener.Invoke(textInvokedOn);
			}
		}

		private static SimpleNameSyntax GetSimpleNameFromNode(AttributeSyntax node)
		{
			var identifierNameSyntax = node.Name as IdentifierNameSyntax;
			var qualifiedNameSyntax = node.Name as QualifiedNameSyntax;

			return
				identifierNameSyntax
				??
				qualifiedNameSyntax?.Right
				??
				(node.Name as AliasQualifiedNameSyntax).Name;
		}

	}
}
