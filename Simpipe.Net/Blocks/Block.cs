namespace Simpipe.Blocks;

public interface IBlock
{
    int InputCount => 0;
    int OutputCount => 0;
    int WorkingCount => 0;
}