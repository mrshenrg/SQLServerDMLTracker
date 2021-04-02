using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;

namespace SQLServerDMLTracker
{
    class DMLTrace
    {
        public static void InitTraceTrig(string connectionString, string tbls)
        {
            CreateTraceLogTable(connectionString);
            DeleteTraceTrig(connectionString, tbls,false);
            CreateTraceTrig(connectionString, tbls);
        }

        public static void CreateTraceLogTable(string connectionString)
        {
            String sql = @"if not exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3) 
            BEGIN 
            CREATE TABLE dbo.Mdl_Trace_Log
            (
	            --autoid int IDENTITY(1,1) not null ,
	            log_guid varchar(50) null ,
	            log_spid int null ,
	            log_time datetime null default(getdate()),
	            log_table varchar(255) null ,
	            log_sql VARCHAR(8000) null ,
	            log_inserted int,
	            log_deleted int,
	            log_updated int ,
	            log_ts timestamp,
                log_pid varchar(48),
                log_hostname nvarchar(256),
                log_programname nvarchar(256)
	            -- CONSTRAINT Mdl_Trace_Log_PK PRIMARY KEY  CLUSTERED
	            -- (
	            --autoid
	            -- )
            )	
            end

            if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_pid') 
                alter table Mdl_Trace_Log add log_pid varchar(48) null 
            if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_hostname') 
                alter table Mdl_Trace_Log add log_hostname nvarchar(256) null
            if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_programname') 
                alter table Mdl_Trace_Log add log_programname nvarchar(256) null

            if not exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Table_Log') and sysstat & 0xf = 3) 
            BEGIN 
            CREATE TABLE dbo.Mdl_Trace_Table_Log
            (
	            log_table varchar(255) null
            )	
            end

            if not exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Record_Log') and sysstat & 0xf = 3) 
            BEGIN 
            CREATE TABLE dbo.Mdl_Trace_Record_Log
            (
	            log_guid varchar(50) null ,
	            log_table varchar(255) null ,
	            log_opt varchar(10) null ,
	            log_record text null
            )	
            end";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        private static void CreateTraceTrig(string connectionString, string tbls)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

            Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2 --name like 'tr_Mdl_Trace_%_log'
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

	        exec('insert into #tmp_Mdl_Trace_tbl(tbl,tr)
	        SELECT name,''tr_Mdl_Trace_'' + name +  ''_log'' from sysobjects 
	        where type=''U''
            " + (tbls == "" ? "" : " and name in (''" + tbls.Replace(",", "'',''") + "'')") + @"')
		
	        DECLARE cursor_tbl CURSOR FOR
	        SELECT tbl,tr from #tmp_Mdl_Trace_tbl where tbl not in ('Mdl_Trace_Log','Mdl_Trace_Table_Log','Mdl_Trace_Record_Log');

	        OPEN cursor_tbl;

	        FETCH NEXT FROM cursor_tbl INTO @tbl,@tr
	        WHILE  @@FETCH_STATUS=0
	        BEGIN
                declare @fld varchar(max) = '', @val varchar(max) = ''
                Select @fld=@fld + ',['+LOWER(a.name)+']',@val=@val + '+(case when '+a.name+' IS null then '',NULL'' else '',''''''+convert(varchar(max),replace('+a.name+','''''''',''""''))+'''''''' end)'
                FROM  syscolumns  a 
                left join systypes b on a.xtype=b.xusertype
                inner join sysobjects d on a.id=d.id and d.xtype='U'
                where b.name not in ('timestamp','text','ntext','image') and d.name = @tbl

	            set @fld = RIGHT(@fld,len(@fld)-1)
	            --set @val = RIGHT(@val,len(@val)-1) --太长，没办法拼接

EXEC('CREATE TRIGGER [dbo].[' + @tr +  '] ON [dbo].[' + @tbl + ']  
FOR DELETE,INSERT,UPDATE
AS 
SET NOCOUNT ON

DECLARE @SPID VARCHAR(20),@GUID VARCHAR(36)
SET @GUID=NEWID()
SET @SPID=CAST(@@SPID AS VARCHAR)

-- 1、获取当前执行的SQL命令
if exists (select * from tempdb.dbo.sysobjects where id = object_id(N''tempdb..#spid_sql'') and type=''U'')
drop table #spid_sql

CREATE TABLE #spid_sql(
	EVENTTYPE VARCHAR(20),
	PARAMETERS INT,
	EVENTINFO VARCHAR(8000)
)

INSERT #spid_sql EXEC(''DBCC INPUTBUFFER (''+@SPID+'')'')

declare @count_deleted int,@count_inserted int,@count_updated int
Select @count_deleted = count(*) from deleted
Select @count_inserted = count(*) from inserted
set @count_updated = 0

if @count_deleted = @count_inserted
begin
	set @count_updated = @count_inserted
	set @count_deleted = 0
	set @count_inserted = 0
end

declare @PID varchar(48),@hostname nvarchar(256),@programname nvarchar(256)
select
@hostname = a.host_name, --客户端主机名称
@programname = a.[program_name], --客户端应用名称
@PID = a.host_process_id --主机进程PID
from sys.dm_exec_sessions a , sys.dm_exec_connections b
where a.session_id=b.session_id and a.session_id = @@SPID

/*select
a.session_id,
a.login_time, --
a.host_name, --客户端主机名称
a.login_name, --登陆名称
b.connection_id , --链接ID
a.[program_name], --客户端应用名称
b.client_net_address, --客户单IP地址
a.host_process_id --主机进程PID
from sys.dm_exec_sessions a , sys.dm_exec_connections b
where a.session_id=b.session_id and a.session_id = @@SPID*/

declare @log_guid varchar(50)
set @log_guid = newid()

-- 2、将当前执行的SQL命令插入日志表
insert into Mdl_Trace_Log(log_guid,log_spid,log_sql,log_table,log_inserted,log_deleted,log_updated,log_pid,log_hostname,log_programname)
SELECT @log_guid,@SPID,EVENTINFO,'''+@tbl+''',@count_inserted,@count_deleted,@count_updated,@PID,@hostname,@programname FROM  #spid_sql

begin try 
-- 3、将当前执行的SQL命令影响行插入日志表
if (exists (Select * from Mdl_Trace_table_Log where log_table='''+@tbl+''')
    and (@count_deleted > 0 or @count_inserted>0 or @count_updated>0))
begin
	declare @fld varchar(max) = '''', @val varchar(max) = ''''
	Select @fld=@fld + '',[''+LOWER(a.name)+ '']'',@val=@val + ''+(case when ''+a.name+'' IS null then '''',NULL'''' else '''',''''''''''''+convert(varchar(max),replace(''+a.name+'','''''''''''''''',''''""''''))+'''''''''''''''' end)''
    FROM  syscolumns  a 
    left join systypes b on a.xtype=b.xusertype
    inner join sysobjects d on a.id=d.id and d.xtype=''U''
    where b.name not in (''timestamp'',''text'',''ntext'',''image'') and d.name = '''+@tbl+'''

	set @fld = RIGHT(@fld,len(@fld)-1)
	set @val = RIGHT(@val,len(@val)-1)

    if (@fld='''+@fld+''')
    begin
        insert into Mdl_Trace_record_Log(log_guid,log_table,log_opt,log_record)
        Select @log_guid,'''+@tbl+''',''fld'','',''+@fld
    
        declare @TableHasIdentity int, @orderno varchar(255)=''''
        Select @TableHasIdentity = OBJECTPROPERTY(OBJECT_ID('''+@tbl+'''),''TableHasIdentity'')
        if (@TableHasIdentity = 1)
            SELECT @orderno = '',''+a.name+'' desc'' FROM sys.identity_columns a INNER JOIN sys.tables b ON a.object_id = b.object_id where b.name = '''+@tbl+'''
        else
	        SELECT @orderno=@orderno+'',''+COLUMN_NAME+'' desc'' FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_NAME='''+@tbl+''' 

        if (isnull(@orderno,'''')<>'''')
            set @orderno = '' order by '' + RIGHT(@orderno,len(@orderno)-1)

	    select '+@fld+' into #deleted from deleted --不能使用text,ntext,image字段，只能外界传入
        exec(''insert into Mdl_Trace_record_Log(log_guid,log_table,log_opt,log_record)
        Select top 20 ''''''+@log_guid+'''''','''''+@tbl+''''',''''deleted'''',''+@val+'' as record_csv from #deleted'' + @orderno)

	    select '+@fld+' into #inserted from inserted
        exec(''insert into Mdl_Trace_record_Log(log_guid,log_table,log_opt,log_record)
        Select top 20 ''''''+@log_guid+'''''','''''+@tbl+''''',''''inserted'''',''+@val+'' as record_csv from #inserted'' + @orderno)
    end
    else 
        insert into Mdl_Trace_record_Log(log_guid,log_table,log_opt,log_record)
        Select @log_guid,'''+@tbl+''',''error'',''跟踪记录字段不匹配，请重建跟踪''
end
end try
BEGIN CATCH
	delete from Mdl_Trace_record_Log where log_guid=@log_guid

    -- 有缺陷，删字段或改字段名，直接就异常了。抛不出来，可能还会导致产品错误
    insert into Mdl_Trace_record_Log(log_guid,log_table,log_opt,log_record)
    Select @log_guid,'''+@tbl+''',''error'',''跟踪记录日志异常，请重建跟踪''
END CATCH

SET NOCOUNT OFF')
		
		FETCH NEXT FROM cursor_tbl INTO @tbl,@tr
	END;
	CLOSE cursor_tbl
	DEALLOCATE cursor_tbl;";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static void DeleteTraceTrig(string connectionString, string tbls, bool bDeleteTraceLog)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

            Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2 --name like 'tr_Mdl_Trace_%_log'
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

            print  '-- 删除触发器 [dbo].[tr_Mdl_Trace_***_log]'

            truncate table #tmp_Mdl_Trace_tbl
            exec('insert into #tmp_Mdl_Trace_tbl(tbl,tr)
            SELECT OBJECT_NAME(parent_id) as tbl,name as tr from sys.triggers 
            where name like ''tr_Mdl_Trace_%_log''
            " + (tbls == "" ? "" : " and OBJECT_NAME(parent_id) in (''" + tbls.Replace(",", "'',''") + "'')") + @"')
	
            DECLARE cursor_tr CURSOR FOR
            Select tbl,tr from #tmp_Mdl_Trace_tbl;

            OPEN cursor_tr;

            FETCH NEXT FROM cursor_tr INTO @tbl,@tr
            WHILE  @@FETCH_STATUS=0
            BEGIN
                -- 删除日志
                --if exists (select * from sysobjects where id = object_id('Mdl_Trace_Log') and sysstat & 0xf = 3)
                --Delete From Mdl_Trace_Log where log_table=@tbl
                --if exists (select * from sysobjects where id = object_id('Mdl_Trace_Table_Log') and sysstat & 0xf = 3)
                --Delete From Mdl_Trace_Table_Log where log_table=@tbl
                --if exists (select * from sysobjects where id = object_id('Mdl_Trace_Record_Log') and sysstat & 0xf = 3)
                --Delete From Mdl_Trace_Record_Log where log_table=@tbl
                
                -- 删除触发器
                set @sql = 'if exists (select * from sysobjects where id = object_id(''[dbo].[' + @tr +  ']'') and sysstat & 0xf = 8)
	            drop trigger [dbo].[' + @tr +  ']'
		
	            exec(@sql)
		
	            FETCH NEXT FROM cursor_tr INTO @tbl,@tr
            END;
            CLOSE cursor_tr
            DEALLOCATE cursor_tr;";

            if (tbls == "" && bDeleteTraceLog)
            sql = sql + @" if exists (select * from sysobjects where id = object_id('Mdl_Trace_Log') and sysstat & 0xf = 3)
	        Drop table Mdl_Trace_Log
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Table_Log') and sysstat & 0xf = 3)
            Drop table  Mdl_Trace_Table_Log
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Record_Log') and sysstat & 0xf = 3)
            Drop table  Mdl_Trace_Record_Log";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static void EnableTraceTrig(string connectionString, string tbls)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

            Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2 --name like 'tr_Mdl_Trace_%_log'
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

            exec('insert into #tmp_Mdl_Trace_tbl(tbl,tr)
	        SELECT OBJECT_NAME(parent_id) as tbl,name as tr from sys.triggers 
	        where name like ''tr_Mdl_Trace_%_log'' 
            " + (tbls == "" ? "" : " and OBJECT_NAME(parent_id) in (''" + tbls.Replace(",", "'',''") + "'')") + @"')
	
	        DECLARE cursor_tr CURSOR FOR
	        SELECT tbl,tr FROM #tmp_Mdl_Trace_tbl;

	        OPEN cursor_tr;

	        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        WHILE  @@FETCH_STATUS=0
	        BEGIN
		        --启用所有表上的所有触发器
		        set @sql = 'ALTER TABLE ['+@tbl+'] enable TRIGGER ['+@tr+'];'
		
		        exec(@sql)
		
		        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        END;
	        CLOSE cursor_tr
	        DEALLOCATE cursor_tr;";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static void DisableTraceTrig(string connectionString, string tbls)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

            Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2 --name like 'tr_Mdl_Trace_%_log'
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

            exec('insert into #tmp_Mdl_Trace_tbl(tbl,tr)
	        SELECT OBJECT_NAME(parent_id) as tbl,name as tr from sys.triggers 
	        where name like ''tr_Mdl_Trace_%_log'' 
	        " + (tbls==""?"": " and OBJECT_NAME(parent_id) in (''" + tbls.Replace(",", "'',''") + "'')") + @"')
	
	        DECLARE cursor_tr CURSOR FOR
	        SELECT tbl,tr FROM #tmp_Mdl_Trace_tbl;

	        OPEN cursor_tr;

	        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        WHILE  @@FETCH_STATUS=0
	        BEGIN
                --禁用所有表上的所有触发器
		        set @sql = 'ALTER TABLE ['+@tbl+'] DISABLE TRIGGER ['+@tr+']; '
		
		        exec(@sql)
		
		        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        END;
	        CLOSE cursor_tr
	        DEALLOCATE cursor_tr;";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static void EnableTraceTrigRecord(string connectionString, string tbls)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

	        SELECT name as tbl,'tr_Mdl_Trace_' + name +  '_log' as tr into #tmp_Mdl_Trace_tbl from sysobjects 
	        where type='U'
            " + (tbls == "" ? "" : " and name in ('" + tbls.Replace(",", "','") + "')") + @"

            --Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers 
            --where name like 'tr_Mdl_Trace_%_log'
            --" + (tbls == "" ? "" : " and OBJECT_NAME(parent_id) in ('" + tbls.Replace(",", "','") + "')") + @"
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

	        DECLARE cursor_tr CURSOR FOR
	        SELECT tbl,tr FROM #tmp_Mdl_Trace_tbl;

	        OPEN cursor_tr;

	        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        WHILE  @@FETCH_STATUS=0
	        BEGIN
		        --启用所有表上的所有触发器跟踪记录
		        set @sql = 'if exists (select * from sysobjects where id = object_id(''Mdl_Trace_Table_Log'') and sysstat & 0xf = 3)
                if not exists (Select * from Mdl_Trace_Table_Log where log_table = '''+@tbl+''')
                insert into Mdl_Trace_Table_Log(log_table) values ('''+@tbl+''')'
		
		        exec(@sql)
		
		        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        END;
	        CLOSE cursor_tr
	        DEALLOCATE cursor_tr;";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static void DisableTraceTrigRecord(string connectionString, string tbls)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl

	        SELECT name as tbl,'tr_Mdl_Trace_' + name +  '_log' as tr into #tmp_Mdl_Trace_tbl from sysobjects 
	        where type='U'
            " + (tbls == "" ? "" : " and name in ('" + tbls.Replace(",", "','") + "')") + @"

            --Select OBJECT_NAME(parent_id) as tbl,name as tr into #tmp_Mdl_Trace_tbl from sys.triggers 
            --where name like 'tr_Mdl_Trace_%_log'
            --" + (tbls == "" ? "" : " and OBJECT_NAME(parent_id) in ('" + tbls.Replace(",", "','") + "')") + @"
	
            declare @tbl varchar(255), @tr varchar(255), @sql varchar(8000)

	        DECLARE cursor_tr CURSOR FOR
	        SELECT tbl,tr FROM #tmp_Mdl_Trace_tbl;

	        OPEN cursor_tr;

	        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        WHILE  @@FETCH_STATUS=0
	        BEGIN
		        --禁用所有表上的所有触发器跟踪记录
		        set @sql = 'if exists (select * from sysobjects where id = object_id(''Mdl_Trace_Table_Log'') and sysstat & 0xf = 3)
                delete from Mdl_Trace_Table_Log where log_table = '''+@tbl+''''
		
		        exec(@sql)
		
		        FETCH NEXT FROM cursor_tr INTO @tbl,@tr
	        END;
	        CLOSE cursor_tr
	        DEALLOCATE cursor_tr;";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }

        public static SqlDataReader QueryTraceTrigList(string connectionString, string tbls, bool bAllTbl)
        {
//            String sql = @"declare @sql varchar(8000)
//            set @sql = 'SELECT  
//		        object_name(a.parent_obj) as [表名]  
//		        ,a.name as [触发器名称]  
//		        ,(case when b.is_disabled=0 then ''启用'' else ''禁用'' end) as [状态]  
//		        ,b.create_date as [创建日期]  
//		        ,b.modify_date as [修改日期]  
//		        --,c.text as [触发器语句]  
//	        FROM sysobjects a  
//		        INNER JOIN sys.triggers b  
//			        ON b.object_id=a.id  
//		        --INNER JOIN syscomments c  
//		        --    ON c.id=a.id  
//	        WHERE a.xtype=''tr'' and a.name like ''tr_Mdl_Trace_%_log'' '
//	        + (case when isnull('" + tblList + @"','')<>'' then ' and OBJECT_NAME(parent_id) in (''" + tblList.Replace(",", "'',''") + @"'')' else '' end) + '
//	        ORDER BY [表名]'
//	
//	        exec(@sql)";

            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl
            select name log_table into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Table_Log') and sysstat & 0xf = 3)
                insert into #tmp_Mdl_Trace_tbl(log_table)
                select distinct log_table from Mdl_Trace_Table_Log

            if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_rd') and type='U')
            drop table #tmp_Mdl_Trace_rd
            select name log_table,0 traceCount,create_date traceMaxtime into #tmp_Mdl_Trace_rd from sys.triggers where 1=2
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
                insert into #tmp_Mdl_Trace_rd(log_table,traceCount,traceMaxtime)
                select log_table,count(*) traceCount,max(log_time) traceMaxtime from Mdl_Trace_Log group by log_table

            SELECT  
                a.name as [表名]  
                ,b.name as [触发器名称]  
                ,(case when b.is_disabled=0 then '启用' else '禁用' end) as [状态] 
                ,(case when t.log_table is null then '' else '启用' end) as [跟踪记录]   
                ,b.create_date as [创建日期]  
                ,b.modify_date as [修改日期]  
                --,c.text as [触发器语句]  
                ,r.traceCount as [日志总数]
                ,r.traceMaxtime as [最近更新]
            FROM sysobjects a  
                LEFT JOIN (Select * from sys.triggers where name like 'tr_Mdl_Trace_%_log') b  
                    ON b.parent_id=a.id  
                LEFT JOIN #tmp_Mdl_Trace_tbl t 
                    ON t.log_table = a.name
                LEFT JOIN #tmp_Mdl_Trace_rd r 
                    ON r.log_table = a.name
                --INNER JOIN syscomments c  
                --    ON c.id=a.id   
            WHERE a.xtype='u' " + (tbls == "" ? "" : " and a.name in ('" + tbls.Replace(",", "','") + "')") 
                                + (bAllTbl ? "" : " and b.name like 'tr_Mdl_Trace_%_log'")
                                + " order by r.traceMaxtime,a.name";

            return MSSQLHelper.ExecuteDataReader(connectionString, sql);
        }

        //模糊查询，查询全部
        public static SqlDataReader QueryTraceTrig(string connectionString, string tbl, bool bAllTbl)
        {
            String sql = @"if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_tbl') and type='U')
            drop table #tmp_Mdl_Trace_tbl
            select name log_table into #tmp_Mdl_Trace_tbl from sys.triggers where 1=2
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Table_Log') and sysstat & 0xf = 3)
                insert into #tmp_Mdl_Trace_tbl(log_table)
                select distinct log_table from Mdl_Trace_Table_Log

            if exists (select * from tempdb.dbo.sysobjects where id = object_id(N'tempdb..#tmp_Mdl_Trace_rd') and type='U')
            drop table #tmp_Mdl_Trace_rd
            select name log_table,0 traceCount,create_date traceMaxtime into #tmp_Mdl_Trace_rd from sys.triggers where 1=2
            if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
                insert into #tmp_Mdl_Trace_rd(log_table,traceCount,traceMaxtime)
                select log_table,count(*) traceCount,max(log_time) traceMaxtime from Mdl_Trace_Log group by log_table

            SELECT  
                a.name as [表名]  
                ,b.name as [触发器名称]  
                ,(case when b.is_disabled=0 then '启用' else '禁用' end) as [状态] 
                ,(case when t.log_table is null then '' else '启用' end) as [跟踪记录]   
                ,b.create_date as [创建日期]  
                ,b.modify_date as [修改日期]  
                --,c.text as [触发器语句]  
                ,r.traceCount as [日志总数]
                ,r.traceMaxtime as [最近更新]
            FROM sysobjects a   
                LEFT JOIN (Select * from sys.triggers where name like 'tr_Mdl_Trace_%_log') b  
                    ON b.parent_id=a.id  
                LEFT JOIN #tmp_Mdl_Trace_tbl t 
                    ON t.log_table = a.name
                LEFT JOIN #tmp_Mdl_Trace_rd r 
                    ON r.log_table = a.name
                --INNER JOIN syscomments c  
                --    ON c.id=a.id  
            WHERE a.xtype='u' " + (tbl == "" ? "" : " and a.name like '%" + tbl + "%'") 
                                + (bAllTbl ? "" : " and b.name like 'tr_Mdl_Trace_%_log'")
                                + " order by r.traceMaxtime desc,a.name";

            return MSSQLHelper.ExecuteDataReader(connectionString, sql);
        }

        //模糊查询，查询全部
        public static SqlDataReader QueryTraceLog(string connectionString, string tbl, bool bViewAlter,string dtEnd, string rowCount)
        {
            String sql = @"if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
            begin 
                if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_pid') 
                    alter table Mdl_Trace_Log add log_pid varchar(48) null 
                if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_hostname') 
                    alter table Mdl_Trace_Log add log_hostname nvarchar(256) null
                if not exists (select c.name,c.id from syscolumns c,sysobjects o  where c.id=o.id and o.xtype='U' and o.name='Mdl_Trace_Log' and c.name ='log_programname') 
                    alter table Mdl_Trace_Log add log_programname nvarchar(256) null

                Select top " + rowCount + @" IDENTITY(int ,1,1) as autoid,log_table,log_sql,log_inserted,log_deleted,log_updated,log_time,log_pid,log_guid,log_hostname,log_programname,log_spid
                into #log
                from Mdl_Trace_Log 
	            where 1=1 " + (tbl == "" ? "" : " and (log_table like '%" + tbl + "%' or log_sql like '%" + tbl + @"%' 
                                                        or log_pid like '%" + tbl + "%' or log_hostname like '%" + tbl + "%' or log_programname like '%" + tbl + "%')")
                            + (dtEnd == "" ? "" : " and (log_time<='" + dtEnd + "')")
                            + (bViewAlter ? " and (log_deleted<>0 or log_inserted<>0 or log_updated<>0)" : "")
	            + @" order by log_ts desc

                select log_table,log_sql,log_inserted,log_deleted,log_updated,log_time,log_pid,log_hostname,log_programname,log_guid,log_spid from #log order by autoid desc

                drop table #log
            end ";

            return MSSQLHelper.ExecuteDataReader(connectionString, sql);
        }

        public static SqlDataReader QueryTraceRecordLog(string connectionString, string logTable, string logGuid)
        {
            String sql = @"if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
            begin 
                if exists (select top 1 * from Mdl_Trace_record_Log where log_guid = '" + logGuid + @"' and log_opt = 'fld')
                begin
                    select 'mdl_trace_record_log_opt' as mdl_trace_record_log_opt, * into #" + logTable + @" from " + logTable + @" where 1=2

	                declare @TableHasIdentity int, @orderno varchar(255)=''
                    Select @TableHasIdentity = OBJECTPROPERTY(OBJECT_ID('" + logTable + @"'),'TableHasIdentity')
                    if @TableHasIdentity = 1
                    set identity_insert  #" + logTable + @"  on

                    if (@TableHasIdentity = 1)
                        SELECT @orderno = ','+a.name FROM sys.identity_columns a INNER JOIN sys.tables b ON a.object_id = b.object_id where b.name = '" + logTable + @"'
                    else
	                    SELECT @orderno=@orderno+','+COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_NAME='" + logTable + @"' 

                    if (isnull(@orderno,'')<>'')
                        set @orderno = ' order by ' + RIGHT(@orderno,len(@orderno)-1) + ',mdl_trace_record_log_opt'

                    declare @tbl varchar(255)='',  @fld varchar(max) = '', @val varchar(max) = '', @opt varchar(max) = '', @sql varchar(max) = ''
                    select @tbl = log_table,@fld = log_record from Mdl_Trace_record_Log where log_guid = '" + logGuid + @"' and log_opt = 'fld'
                    set @fld = RIGHT(@fld,len(@fld)-1)
                    
	                DECLARE cursor_tr CURSOR FOR
	                Select log_record,log_opt from Mdl_Trace_record_Log where log_guid = '" + logGuid + @"' and log_opt <> 'fld';

	                OPEN cursor_tr;

	                FETCH NEXT FROM cursor_tr INTO @val,@opt
	                WHILE  @@FETCH_STATUS=0
	                BEGIN
                        set @val = RIGHT(@val,len(@val)-1)
		                set @sql = 'insert into #" + logTable + @"(mdl_trace_record_log_opt,'+@fld+') values ('''+@opt+''','+@val+')'
		                exec(@sql)
		
		                FETCH NEXT FROM cursor_tr INTO @val,@opt
	                END;
	                CLOSE cursor_tr
	                DEALLOCATE cursor_tr;

                    if @TableHasIdentity = 1
                    set identity_insert  #" + logTable + @"  off
                
                    exec ('Select mdl_trace_record_log_opt as logOpt,'+@fld+' from #" + logTable + @"' + @orderno)
                end
                else
                    select * from Mdl_Trace_record_Log where log_guid = '" + logGuid + @"'
            end ";

            return MSSQLHelper.ExecuteDataReader(connectionString, sql);
        }

        public static void TruncateTraceLog(string connectionString, string tbls)
        {
            String sql = "";
            if (tbls == "")
                sql = @"if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
                truncate table  Mdl_Trace_Log

                if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Record_Log') and sysstat & 0xf = 3)
                truncate table  Mdl_Trace_Record_Log";
            else
                sql = @"if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Log') and sysstat & 0xf = 3)
                delete from Mdl_Trace_Log where log_table in ('" + tbls.Replace(",", "','") + @"')

                if exists (select * from sysobjects where id = object_id('dbo.Mdl_Trace_Record_Log') and sysstat & 0xf = 3)
                delete from Mdl_Trace_Record_Log where log_table in ('" + tbls.Replace(",", "','") + "')";

            MSSQLHelper.ExecuteNonQuery(connectionString, sql);
        }
    }
}
