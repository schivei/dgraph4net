namespace Dgraph4Net;

public interface IPaginationFunctions
{
    void First(long first);

    void Offset(long offset);

    void After(Uid after);
}
