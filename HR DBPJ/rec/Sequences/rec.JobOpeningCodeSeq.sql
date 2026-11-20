-- Running number of JOB-yyyy-nnn opening codes (the API formats it; it does not reset each year).
CREATE SEQUENCE [rec].[JobOpeningCodeSeq]
    AS int
    START WITH 1
    INCREMENT BY 1
    MINVALUE 1
    NO CYCLE
    CACHE 10;
