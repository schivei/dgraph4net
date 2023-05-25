//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Dgraph4Net.ActiveRecords;

///*
//# dgraph samples

//# '#' is used for comments

//# Define Types

//type Person {
//    name
//    boss_of
//    works_for
//}

//type Company {
//    name
//    industry
//    <~works_for> #this is an alias
//}

//# Define Directives and index

//industry: string @index(term) .
//boss_of: [uid] .
//name: string @index(exact, term) .
//works_for: [uid] @reverse .
// */
///*
//// csharp sample

//// Uid type is already mapped to follow dgraph uid type, do not change it
//// attributes is already implemented, do not change it
//// types
//public abstract class DEntity
//{
//    [CommonPredicate("uid")]
//    public Uid Uid {get;set;}
//    [CommonPredicate("dgraph.type")]
//    public HashSet<string> Types {get;set;}
//}

//[DgraphType("Person")]
//public sealed class Person : DEntity
//{
//    [StringPredicate("name", Exact = true, Term = true)]
//    public string Name {get;set;}
//    [PredicateReferencesTo<Person>("boss_of")]
//    public HashSet<Person> BossOf {get;set;}
//    [PredicateReferencesTo<Company>("works_for"), ReversePredicate]
//    public HashSet<Company> WorksFor {get;set;}
//}
//[DgraphType("Company")]
//public sealed class Company : DEntity
//{
//    [StringPredicate("name", Exact = true, Term = true)]
//    public string Name {get;set;}
//    [StringPredicate("industry", Term = true)]
//    public string Industry {get;set;}
//    [CommonPredicate("~works_for")]
//    public HashSet<Person> WorksHere {get;set;}
//}
// */
///*
//# dgraph sample query 1

//query ($abc: string = "abc"){
//    q0(func: type(Person)) @filter(allofterms(Person.name, $abc)) {
//        uid
//        name
//        boss_of {
//            uid
//        }
//        works_for {
//            uid
//        }
//        dgraph.type
//    }
//}

//# dgraph sample query 2

//query ($abc: string = "abc"){
//    q0(func: type(Person)) @filter(anyofterms(name, $abc)) {
//        uid
//        name
//        boss_of {
//            uid
//            name
//            boss_of {
//                uid
//            }
//            works_for {
//                uid
//            }
//            dgraph.type
//        }
//        works_for {
//            uid
//            name
//            industry
//            ~works_for {
//                uid
//            }
//            dgraph.type
//        }
//        dgraph.type
//    }
//}

//# dgraph sample query 3

//query ($abc: string = "abc"){
//    var (func: eq(name, $abc)) {
//        u as uid
//    }
    
//    q0(func: type(Person)) @filter(uid(u)) {
//        uid
//        name
//        boss_of {
//            uid
//            name
//            boss_of {
//                uid
//            }
//            works_for {
//                uid
//            }
//            dgraph.type
//        }
//        works_for {
//            uid
//            name
//            industry
//            ~works_for {
//                uid
//            }
//            dgraph.type
//        }
//        dgraph.type
//    }
//    q1(func: type(Company)) @filter(uid(u)) {
//        uid
//        name
//        industry
//        ~works_for {
//            uid
//        }
//        dgraph.type
//    }
//}
// */

//public static class Functions
//{
//    public static object Val(object variable) => variable;
//    public static Uid Uid(object variable) => variable is Uid u ? u : System.Uid.NewUid();
//    public static long Count(string variable) => variable.Length;
//    public static bool Not(bool what) => !what;
//    public static bool Or(bool left, bool right) => left || right;
//    public static bool And(bool left, bool right) => left && right;
//}

//public static class Linq
//{
//    public static IDgraphQueryable Vars(this IDgraph4NetClient client, Dictionary<string, object>? vars = null)
//    {

//    }
//}

//public interface IDgraphQueryable
//{
//    IDgraphQueryable SetVar();

//    IDgraphQueryable Filter();

//    Task<IEnumerable<T>> SingleQueryAsync<T>();
//    Task<IMultiResponse> MultiQueryAsync(params Expression<Action<IFilterType<IEntity>>>[] expressions);
//}

//public interface IFilterType<T>
//{
//    bool AllOfTerms(Expression<Func<T, string>> predicate, string value);
//    bool AnyOfTerms(Expression<Func<T, string>> predicate, string value);
//    bool AllOfText(Expression<Func<T, string>> predicate, string value);
//    bool Eq(Expression<Func<T, object>> predicate, string value);
//    bool Lt(Expression<Func<T, object>> predicate, string value);
//    bool Le(Expression<Func<T, object>> predicate, string value);
//    bool Gt(Expression<Func<T, object>> predicate, string value);
//    bool Ge(Expression<Func<T, object>> predicate, string value);
//    bool Has(Expression<Func<T, object>> predicate);
//    bool Regexp(Expression<Func<T, string>> predicate, Regex value);
//    bool Match(Expression<Func<T, string>> predicate, string value, int distance);
//    bool Include<TE>(Expression<Func<T, TE>> predicate, Expression<Func<IFilterType<TE>, bool>> filter);
//    bool Select(Expression<Func<T, object>> predicate);
//}

//public interface IMultiResponse
//{
//    object this[int index] { get; }
//    IEnumerable<T> Get<T>();
//    T? Single<T>();
//}
