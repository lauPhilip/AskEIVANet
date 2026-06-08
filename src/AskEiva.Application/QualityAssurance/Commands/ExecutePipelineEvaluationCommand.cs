using System;
using System.Collections.Generic;
using MediatR;
using AskEiva.Domain.Repositories;

namespace AskEiva.Application.QualityAssurance.Commands;

// Core Evaluation Suite Payloads
public record ExecutePipelineEvaluationCommand(List<EvaluationTestCase> TestSuite) : IRequest<EvaluationSummaryReport>;
public record EvaluationTestCase(string Query, List<string> ExpectedContextKeys, string GroundTruthAnswer);
public record EvaluationSummaryReport(double AverageContextPrecision, double AverageAnswerFaithfulness, List<EvaluationRowResult> DetailedResults);
public record EvaluationRowResult(string Query, double ContextPrecisionScore, double FaithfulnessScore, string GeneratedAnswer, bool PassedStrictVersionCheck);

// Swipe Action Command Payload for Reinforcement Telemetry
public record SubmitSwipeFeedbackCommand(
    string Query, 
    string GeneratedAnswer, 
    bool IsApproved, 
    string TargetCollectionBias, // "ClosedBaseline", "LiveOpenAssist"
    string CorrectionNotes = ""
) : IRequest<bool>;