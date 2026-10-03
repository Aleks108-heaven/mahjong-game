namespace TestMahjongGame.Engine;

public enum TurnState
{
    Draw,
    ActionPhase,
    Discard,
    WaitPhase,
    NextPlayer,

    // Paused: the human may call the last discard (pon or chi) or pass.
    CallDecision
}
