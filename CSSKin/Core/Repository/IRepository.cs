namespace CSSKin.Core.Repository;

public interface IRepository<T>
{
    T? Create(T data);
    void Delete(string uuid);
    List<T> GetAll();
    List<T> Get(string uuid);
    void UpdateOne(T data);
}