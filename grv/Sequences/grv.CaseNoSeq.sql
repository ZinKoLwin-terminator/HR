-- Running number of GRV-yyyy-nnnnn case numbers (the API formats it when a draft is submitted; it does not reset each year).
CREATE SEQUENCE [grv].[CaseNoSeq]
    AS int
    START WITH 1
    INCREMENT BY 1
    MINVALUE 1
    NO CYCLE
    CACHE 10;
