-- Running number of LV-yyyy-nnnn application numbers (the API formats it; it does not reset each year).
CREATE SEQUENCE [lve].[LeaveApplicationNoSeq]
    AS int
    START WITH 1
    INCREMENT BY 1
    MINVALUE 1
    NO CYCLE
    CACHE 10;
