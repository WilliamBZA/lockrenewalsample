if not exists(select 1 from sys.schemas where name = 'nsb')
    exec('create schema nsb;');
