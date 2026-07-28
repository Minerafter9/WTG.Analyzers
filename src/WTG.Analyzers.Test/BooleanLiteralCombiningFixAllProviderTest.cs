using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using WTG.Analyzers.TestFramework;

namespace WTG.Analyzers.Test
{
	[TestFixture]
	public class BooleanLiteralCombiningFixAllProviderTest
	{
		[TestCase("NoAutoFix")]
		[TestCase("WithPreprocessorDirectives")]
		public async Task BulkUpdateSkipsNonAutoFixableDiagnostics(string sampleName)
		{
			var data = Samples.Single(x => x.Name == sampleName);
			var analyzer = new BooleanLiteralCombiningAnalyzer();
			var codeFix = new BooleanLiteralCombiningCodeFixProvider();
			var document = ModelUtils.CreateDocument(data);
			var diagnostics = await DiagnosticUtils.GetDiagnosticsAsync(analyzer, document).ConfigureAwait(false);

			Assert.That(diagnostics, Is.Not.Empty);
			Assert.That(diagnostics.All(IsNotAutoFixable), Is.True);

			var individualActions = new List<Tuple<Diagnostic, CodeAction>>();

			foreach (var diagnostic in diagnostics)
			{
				await CodeFixUtils.CollectCodeActions(codeFix, document, diagnostic, individualActions).ConfigureAwait(false);
			}

			Assert.That(individualActions, Is.Empty);

			var fixAllContext = new FixAllContext(
				document,
				codeFix,
				FixAllScope.Document,
				"SimplifyCombinedBoolLiteral",
				codeFix.FixableDiagnosticIds,
				new TestDiagnosticProvider(diagnostics),
				CancellationToken.None);
			var fixAll = await codeFix.GetFixAllProvider().GetFixAsync(fixAllContext).ConfigureAwait(false);
			var operations = await fixAll.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
			var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
			var changedDocument = changedSolution.GetDocument(document.Id);
			var actual = await changedDocument.GetTextAsync().ConfigureAwait(false);

			Assert.That(actual.ToString(), Is.EqualTo(data.Source));
		}

		static bool IsNotAutoFixable(Diagnostic diagnostic)
			=> diagnostic.Properties.TryGetValue(CanAutoFixProperty, out var value) && value == bool.FalseString;

		const string CanAutoFixProperty = "CanAutoFix";
		const string TestDataPrefix = "WTG.Analyzers.Test.TestData.BooleanLiteralCombiningAnalyzer.";

		static IEnumerable<SampleDataSet> Samples => SampleDataSet.GetSamples(
			typeof(BooleanLiteralCombiningFixAllProviderTest).GetTypeInfo().Assembly,
			TestDataPrefix);

		sealed class TestDiagnosticProvider : FixAllContext.DiagnosticProvider
		{
			public TestDiagnosticProvider(Diagnostic[] diagnostics)
			{
				this.diagnostics = Task.FromResult(diagnostics.AsEnumerable());
			}

			public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) => diagnostics;
			public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) => diagnostics;
			public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) => diagnostics;

			readonly Task<IEnumerable<Diagnostic>> diagnostics;
		}
	}
}
