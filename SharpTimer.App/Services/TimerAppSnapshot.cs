using SharpTimer.Core.Models;
using SharpTimer.Core.Statistics;
using SharpTimer.Core.Timer;
using System.Collections.Generic;

namespace SharpTimer.App.Services;

public sealed record TimerAppSnapshot(
    Session CurrentSession,
    IReadOnlyList<Session> Sessions,
    TimerSnapshot Timer,
    IReadOnlyList<Solve> Solves,
    SolveStatistics Statistics);
