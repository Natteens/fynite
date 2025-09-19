using System;
using System.Collections.Generic;
using System.Threading;

public class TransitionSequencer
{
    public readonly StateMachine Machine;

    private ISequence sequencer;
    Action nextPhase;
    (State from, State to)? pending;
    State lastFrom, lastTo;
    public TransitionSequencer(StateMachine machine)
    {
        Machine = machine;
    }
    
    public void RequestTransition(State from, State to)
    {
       // Machine.ChangeState(from, to);
       if(to == null || from == null) return;
       if(sequencer != null) { pending = (from,to); return; }
       BeginTransition(from, to);
       
    }

    static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool deactivate)
    {
        var steps = new List<PhaseStep>();
        for (int i = 0; i < chain.Count; i++)
        {
            var acts = chain[i].Activities;
            for (int j = 0; j < acts.Count; j++)
            {
                var a = acts[j];
                if (deactivate)
                {
                    if(a.Mode == ActivityMode.Active) steps.Add(ct => a.DeactivateAsync(ct));
                }
                else
                {
                    if(a.Mode == ActivityMode.Inactive) steps.Add(ct => a.ActivateAsync(ct));
                }
            }
        }
        return steps;
    }
    
    static List<State> StatesToExit(State from, State lca)
    {
        var list = new List<State>();
        for (var s = from; s != null && s != lca; s = s.Parent) list.Add(s);
        return list;
    }

    static List<State> StatesToEnter(State to, State lca)
    {
        var stack = new Stack<State>();
        for(var s = to; s != lca; s = s.Parent) stack.Push(s);
        return new List<State>(stack);
    }

    private CancellationTokenSource cts;
    public readonly bool UseSequential = true; // Set to false to use parallel 
    
    void BeginTransition(State from, State to)
    {
        var lca = Lca(from, to);
        var exitChain = StatesToExit(from, lca);
        var enterChain = StatesToEnter(to, lca);
        var exitSteps = GatherPhaseSteps(exitChain, deactivate: true);
        
        sequencer = UseSequential ? new SequentialPhase(exitSteps, cts.Token) : new ParallelPhase(exitSteps, cts.Token);
        sequencer.Start();

        nextPhase = () =>
        {
            Machine.ChangeState(from, to);
            
            var enterSteps = GatherPhaseSteps(enterChain, deactivate: false);
            sequencer = UseSequential ? new SequentialPhase(enterSteps, cts.Token) : new ParallelPhase(enterSteps, cts.Token);
            sequencer.Start();
        };
    }
    
    void EndTransition()
    {
        sequencer = null;
        if (pending.HasValue)
        {
            (State from, State to) p = pending.Value;
            pending = null;
            BeginTransition(p.from, p.to);
        }
    }

    public void Tick(float deltaTime)
    {
        if (sequencer != null)
        {
            if(sequencer.Update())
            {
                if (nextPhase != null)
                {
                    var n = nextPhase;
                    nextPhase = null;
                    n();
                }
                else
                {
                    EndTransition();
                }
            }
            return;
        }
        Machine.InternalTick(deltaTime);
    }
    public static State Lca(State a, State b)
    {
        var ap = new HashSet<State>();
        for (var s = a; s != null; s = s.Parent) ap.Add(s);
        for (var s = b; s != null; s = s.Parent)
            if (ap.Contains(s))
                return s;
        return null;
    }
}